using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace ContainerTweaks.Patch
{
    internal static class MpCompat
    {
        private const ushort QuickTransferRequestMessageId = 30995;
        private const ushort QuickLoadMagazineRequestMessageId = 30996;

        private static ManualLogSource Logger;
        private static Harmony harmony;

        private static Type krokoshaScavType;
        private static Type itemSyncType;
        private static Type netObjectRegistryType;
        private static Type netType;
        private static Type serverMainType;
        private static Type clientMainType;
        private static Type netPlayerType;
        private static Type networkDelegateType;
        private static Type netDataReaderType;
        private static Type netDataWriterType;
        private static Type deliveryMethodType;
        private static Type netExtensionsType;
        private static FieldInfo netPlayerLocalPlayerField;
        private static FieldInfo netPlayerBodyToPlayerDictField;
        private static MethodInfo netPlayerLookupMethod;
        private static FieldInfo inventoryActionFlagField;
        private static FieldInfo containerStatusChangedField;

        private static bool resolutionAttempted;
        private static int resolvedAssemblyCount = -1;
        private static bool pluginPresent;
        private static int networkStateFrame = -1;
        private static bool cachedNetworkRunning;
        private static bool cachedIsClient;
        private static bool cachedIsServer;
        private static bool cachedWorldGenerated;
        private static float nextInactiveNetworkRefreshTime;
        private static bool quickTransferRequestRegistered;
        private static bool quickLoadMagazineRequestRegistered;

        public static bool IsPluginPresent
        {
            get
            {
                EnsureResolved();
                return pluginPresent;
            }
        }

        public static bool IsActive
        {
            get { return IsNetworkRunning; }
        }

        public static bool IsNetworkRunning
        {
            get
            {
                RefreshNetworkState();
                return cachedNetworkRunning;
            }
        }

        public static bool IsClient
        {
            get
            {
                RefreshNetworkState();
                return cachedIsClient;
            }
        }

        public static bool IsServer
        {
            get
            {
                RefreshNetworkState();
                return cachedIsServer;
            }
        }

        public static bool IsWorldGenerated
        {
            get
            {
                RefreshNetworkState();
                return cachedWorldGenerated;
            }
        }

        public static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
            harmony = new Harmony("com.user.containertweaks.mpcompat");
            EnsureResolved();
            EnsureRuntimeHooksInstalled();
        }

        public static bool TryHandleQuickTransfer(PlayerCamera camera, ContainerTweaksPatcher.TransferType transferType, Item dragItem, Item targetItem)
        {
            if (!IsActive || camera == null || dragItem == null || targetItem == null)
            {
                return false;
            }

            EnsureRuntimeHooksInstalled();

            uint dragSyncId;
            uint targetSyncId;
            if (!TryEnsureItemSyncId(dragItem, out dragSyncId) || !TryEnsureItemSyncId(targetItem, out targetSyncId))
            {
                WarnOnce("Quick transfer skipped because Krokosha sync ids were unavailable.");
                return false;
            }

            if (IsClient && !IsServer)
            {
                Body predictedBody = camera.body;
                if (predictedBody != null)
                {
                    ContainerTweaksPatcher.ExecuteQuickTransfer(predictedBody, transferType, dragItem, targetItem, false);
                }

                return SendQuickTransferRequest(transferType, dragSyncId, targetSyncId);
            }

            Body body = camera.body;
            if (body == null)
            {
                return false;
            }

            return ContainerTweaksPatcher.ExecuteQuickTransfer(body, transferType, dragItem, targetItem, true);
        }

        public static bool TryHandleQuickLoadMagazine(AmmoScript magazine, AmmoScript ammo)
        {
            if (!IsActive || magazine == null || ammo == null)
            {
                return false;
            }

            EnsureRuntimeHooksInstalled();

            Item magazineItem = magazine.GetComponent<Item>();
            Item ammoItem = ammo.GetComponent<Item>();
            uint magazineSyncId;
            uint ammoSyncId;
            if (!TryEnsureItemSyncId(magazineItem, out magazineSyncId) || !TryEnsureItemSyncId(ammoItem, out ammoSyncId))
            {
                WarnOnce("Quick magazine load skipped because Krokosha sync ids were unavailable.");
                return false;
            }

            if (IsClient && !IsServer)
            {
                Body predictedBody = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                if (predictedBody != null)
                {
                    ContainerTweaksPatcher.ExecuteQuickLoadMagazine(predictedBody, magazine, ammo, false);
                }

                return SendQuickLoadMagazineRequest(magazineSyncId, ammoSyncId);
            }

            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            return ContainerTweaksPatcher.ExecuteQuickLoadMagazine(body, magazine, ammo, true);
        }

        public static IDisposable BeginInventoryMutation(Body body)
        {
            if (!IsActive || !IsWorldGenerated)
            {
                return NullScope.Instance;
            }

            object previous = null;
            bool hadFlag = false;
            try
            {
                InvokeStatic(itemSyncType, "Client_GetReadyToSyncInventoryState", new object[] { body });
                if (inventoryActionFlagField != null)
                {
                    previous = inventoryActionFlagField.GetValue(null);
                    hadFlag = true;
                    inventoryActionFlagField.SetValue(null, true);
                }
            }
            catch
            {
            }

            return new InventoryMutationScope(hadFlag, previous);
        }

        public static void MarkItemChanged(Item item, bool containerChanged)
        {
            if (!IsActive || item == null)
            {
                return;
            }

            object syncInfo;
            if (!TryGetSyncInfo(item, out syncInfo) || syncInfo == null)
            {
                TryEnsureItemSync(item);
                TryGetSyncInfo(item, out syncInfo);
            }

            if (syncInfo == null)
            {
                return;
            }

            InvokeInstance(syncInfo, "SetIgnoreTimeForRoundTrip", Array.Empty<object>());
            if (IsServer)
            {
                InvokeStatic(netObjectRegistryType, "ForceAddFastSync", new object[] { syncInfo });
                InvokeStatic(netObjectRegistryType, "Server_ObjectSyncSingle", new object[] { syncInfo, true });
            }

            if (containerChanged)
            {
                AddContainerStatusChanged(syncInfo);
            }
        }

        private static bool SendQuickTransferRequest(ContainerTweaksPatcher.TransferType transferType, uint dragSyncId, uint targetSyncId)
        {
            return SendSimpleUintUintByteToServer(QuickTransferRequestMessageId, dragSyncId, targetSyncId, (byte)transferType, true);
        }

        private static bool SendQuickLoadMagazineRequest(uint magazineSyncId, uint ammoSyncId)
        {
            return SendSimpleUintUintToServer(QuickLoadMagazineRequestMessageId, magazineSyncId, ammoSyncId, true);
        }

        private static void EnsureRuntimeHooksInstalled()
        {
            if (!pluginPresent)
            {
                return;
            }

            RegisterQuickTransferRequestReceiver();
            RegisterQuickLoadMagazineRequestReceiver();
        }

        private static void RegisterQuickTransferRequestReceiver()
        {
            if (quickTransferRequestRegistered)
            {
                return;
            }

            EnsureResolved();
            if (serverMainType == null || networkDelegateType == null || netDataReaderType == null)
            {
                return;
            }

            MethodInfo methodInfo = FindStaticMethod(serverMainType, "RegisterServerReciever", new Type[]
            {
                typeof(ushort),
                networkDelegateType
            });
            if (methodInfo == null)
            {
                return;
            }

            try
            {
                object receiver = CreateQuickTransferRequestReceiverDelegate();
                methodInfo.Invoke(null, new object[] { QuickTransferRequestMessageId, receiver });
                quickTransferRequestRegistered = true;
            }
            catch (Exception ex)
            {
                LogWarning("Failed to register quick transfer receiver: " + ex.Message);
            }
        }

        private static void RegisterQuickLoadMagazineRequestReceiver()
        {
            if (quickLoadMagazineRequestRegistered)
            {
                return;
            }

            EnsureResolved();
            if (serverMainType == null || networkDelegateType == null || netDataReaderType == null)
            {
                return;
            }

            MethodInfo methodInfo = FindStaticMethod(serverMainType, "RegisterServerReciever", new Type[]
            {
                typeof(ushort),
                networkDelegateType
            });
            if (methodInfo == null)
            {
                return;
            }

            try
            {
                object receiver = CreateQuickLoadMagazineRequestReceiverDelegate();
                methodInfo.Invoke(null, new object[] { QuickLoadMagazineRequestMessageId, receiver });
                quickLoadMagazineRequestRegistered = true;
            }
            catch (Exception ex)
            {
                LogWarning("Failed to register quick magazine load receiver: " + ex.Message);
            }
        }

        private static object CreateQuickTransferRequestReceiverDelegate()
        {
            DynamicMethod dynamicMethod = new DynamicMethod("ContainerTweaks_QuickTransferRequestReceiver", typeof(void), new Type[]
            {
                typeof(uint),
                netDataReaderType.MakeByRefType()
            }, typeof(MpCompat).Module, true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Call, typeof(MpCompat).GetMethod("HandleQuickTransferRequestMessage", BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate(networkDelegateType);
        }

        private static object CreateQuickLoadMagazineRequestReceiverDelegate()
        {
            DynamicMethod dynamicMethod = new DynamicMethod("ContainerTweaks_QuickLoadMagazineRequestReceiver", typeof(void), new Type[]
            {
                typeof(uint),
                netDataReaderType.MakeByRefType()
            }, typeof(MpCompat).Module, true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Call, typeof(MpCompat).GetMethod("HandleQuickLoadMagazineRequestMessage", BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate(networkDelegateType);
        }

        private static void HandleQuickTransferRequestMessage(uint senderClientId, object reader)
        {
            try
            {
                if (!IsNetworkRunning || !IsServer || reader == null)
                {
                    return;
                }

                uint dragSyncId = ReadUInt(reader);
                uint targetSyncId = ReadUInt(reader);
                byte transferTypeByte = ReadByte(reader);
                ContainerTweaksPatcher.TransferType transferType = (ContainerTweaksPatcher.TransferType)transferTypeByte;

                Body body = GetBodyFromClientId(senderClientId);
                Item dragItem = FindItemBySyncId(dragSyncId);
                Item targetItem = FindItemBySyncId(targetSyncId);
                if (body == null || dragItem == null || targetItem == null)
                {
                    return;
                }

                if (!IsItemOwnedByBodyOrContainer(body, dragItem))
                {
                    return;
                }

                ContainerTweaksPatcher.ExecuteQuickTransfer(body, transferType, dragItem, targetItem, true);
            }
            catch (Exception ex)
            {
                LogWarning("Quick transfer request failed: " + ex.Message);
            }
        }

        private static void HandleQuickLoadMagazineRequestMessage(uint senderClientId, object reader)
        {
            try
            {
                if (!IsNetworkRunning || !IsServer || reader == null)
                {
                    return;
                }

                uint magazineSyncId = ReadUInt(reader);
                uint ammoSyncId = ReadUInt(reader);
                Body body = GetBodyFromClientId(senderClientId);
                Item magazineItem = FindItemBySyncId(magazineSyncId);
                Item ammoItem = FindItemBySyncId(ammoSyncId);
                AmmoScript magazine = magazineItem != null ? magazineItem.GetComponent<AmmoScript>() : null;
                AmmoScript ammo = ammoItem != null ? ammoItem.GetComponent<AmmoScript>() : null;
                if (body == null || magazine == null || ammo == null)
                {
                    return;
                }

                if (!IsItemOwnedByBodyOrContainer(body, ammo))
                {
                    return;
                }

                ContainerTweaksPatcher.ExecuteQuickLoadMagazine(body, magazine, ammo, true);
            }
            catch (Exception ex)
            {
                LogWarning("Quick magazine load request failed: " + ex.Message);
            }
        }

        private static bool TryEnsureItemSyncId(Item item, out uint syncId)
        {
            syncId = 0U;
            if (item == null)
            {
                return false;
            }

            object syncInfo;
            if (!TryGetSyncInfo(item, out syncInfo) || syncInfo == null)
            {
                TryEnsureItemSync(item);
                TryGetSyncInfo(item, out syncInfo);
            }

            return TryGetSyncId(syncInfo, out syncId);
        }

        private static void TryEnsureItemSync(Item item)
        {
            if (netObjectRegistryType == null || item == null)
            {
                return;
            }

            try
            {
                object[] parameters = new object[] { item, null };
                MethodInfo methodInfo = FindStaticMethod(netObjectRegistryType, "TryGetSyncInfoOrRegister", new Type[]
                {
                    typeof(Component),
                    null
                });
                if (methodInfo != null)
                {
                    methodInfo.Invoke(null, parameters);
                }
            }
            catch
            {
            }
        }

        private static bool TryGetSyncInfo(Item item, out object syncInfo)
        {
            syncInfo = null;
            if (itemSyncType == null || item == null)
            {
                return false;
            }

            try
            {
                object[] parameters = new object[] { item, null };
                MethodInfo methodInfo = FindStaticMethod(itemSyncType, "TryGetSyncInfo", new Type[]
                {
                    typeof(Item),
                    null
                });
                if (methodInfo == null)
                {
                    return false;
                }

                object result = methodInfo.Invoke(null, parameters);
                syncInfo = parameters[1];
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetSyncId(object syncInfo, out uint syncId)
        {
            syncId = 0U;
            if (syncInfo == null)
            {
                return false;
            }

            try
            {
                FieldInfo field = syncInfo.GetType().GetField("syncid", BindingFlags.Instance | BindingFlags.Public);
                if (field != null)
                {
                    object value = field.GetValue(syncInfo);
                    if (value is uint)
                    {
                        syncId = (uint)value;
                        return true;
                    }
                }

                PropertyInfo property = syncInfo.GetType().GetProperty("syncid", BindingFlags.Instance | BindingFlags.Public);
                if (property != null)
                {
                    object value = property.GetValue(syncInfo, null);
                    if (value is uint)
                    {
                        syncId = (uint)value;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static Item FindItemBySyncId(uint syncId)
        {
            if (syncId == 0U)
            {
                return null;
            }

            Item[] items = UnityEngine.Object.FindObjectsOfType<Item>();
            foreach (Item item in items)
            {
                object syncInfo;
                uint id;
                if (TryGetSyncInfo(item, out syncInfo) && TryGetSyncId(syncInfo, out id) && id == syncId)
                {
                    return item;
                }
            }

            return null;
        }

        private static bool IsItemOwnedByBodyOrContainer(Body body, Component itemComponent)
        {
            if (body == null || itemComponent == null)
            {
                return false;
            }

            Transform root = body.transform;
            Transform current = itemComponent.transform;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        private static void RefreshNetworkState()
        {
            EnsureResolved();
            if (!pluginPresent)
            {
                cachedNetworkRunning = false;
                cachedIsClient = false;
                cachedIsServer = false;
                cachedWorldGenerated = false;
                return;
            }

            if (!cachedNetworkRunning && Time.unscaledTime < nextInactiveNetworkRefreshTime)
            {
                return;
            }

            if (networkStateFrame == Time.frameCount)
            {
                return;
            }

            networkStateFrame = Time.frameCount;
            cachedNetworkRunning = ReadStaticBool(krokoshaScavType, "network_system_is_running");
            if (!cachedNetworkRunning)
            {
                cachedIsClient = false;
                cachedIsServer = false;
                cachedWorldGenerated = false;
                nextInactiveNetworkRefreshTime = Time.unscaledTime + 0.5f;
                return;
            }

            cachedIsClient = ReadStaticBool(krokoshaScavType, "is_client");
            cachedIsServer = ReadStaticBool(krokoshaScavType, "is_server");
            cachedWorldGenerated = InvokeStaticBool(krokoshaScavType, "IsNetworkActiveAndIsWorldGenerated");
            nextInactiveNetworkRefreshTime = 0f;
            EnsureRuntimeHooksInstalled();
        }

        private static void EnsureResolved()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (resolutionAttempted && assemblies.Length == resolvedAssemblyCount)
            {
                return;
            }

            resolutionAttempted = true;
            resolvedAssemblyCount = assemblies.Length;
            krokoshaScavType = FindType(assemblies, "KrokoshaCasualtiesMP.KrokoshaScavMultiplayer");
            itemSyncType = FindType(assemblies, "KrokoshaCasualtiesMP.ItemSync");
            netObjectRegistryType = FindType(assemblies, "KrokoshaCasualtiesMP.NetObjectRegistry");
            netType = FindType(assemblies, "KrokoshaCasualtiesMP.Net");
            serverMainType = FindType(assemblies, "KrokoshaCasualtiesMP.ServerMain");
            clientMainType = FindType(assemblies, "KrokoshaCasualtiesMP.ClientMain");
            netPlayerType = FindType(assemblies, "KrokoshaCasualtiesMP.NetPlayer");
            netExtensionsType = FindType(assemblies, "KrokoshaCasualtiesMP.MyLiteNetLibExtensions");

            Type inventoryActionPatchType = FindType(assemblies, "KrokoshaCasualtiesMP.PlayerCamera_TryPerformInventoryAction_MultiplayerPatch");
            inventoryActionFlagField = inventoryActionPatchType != null ? inventoryActionPatchType.GetField("is_inside_TryPerformInventoryAction", BindingFlags.Static | BindingFlags.Public) : null;
            containerStatusChangedField = itemSyncType != null ? itemSyncType.GetField("ContainerStatusChanged", BindingFlags.Static | BindingFlags.Public) : null;
            netPlayerLookupMethod = netPlayerType != null ? netPlayerType.GetMethod("TryGetNetPlayerAndBodyFromClientId", BindingFlags.Static | BindingFlags.Public) : null;
            netPlayerLocalPlayerField = netPlayerType != null ? netPlayerType.GetField("LOCAL_PLAYER", BindingFlags.Static | BindingFlags.Public) : null;
            netPlayerBodyToPlayerDictField = netPlayerType != null ? netPlayerType.GetField("BodyToPlayerDict", BindingFlags.Static | BindingFlags.Public) : null;

            Type type = krokoshaScavType;
            networkDelegateType = type != null ? type.GetNestedType("KrokoshaHandleNamedMessageDelegate", BindingFlags.Public | BindingFlags.NonPublic) : null;
            netDataReaderType = FindType(assemblies, "LiteNetLib.Utils.NetDataReader");
            netDataWriterType = FindType(assemblies, "LiteNetLib.Utils.NetDataWriter");
            deliveryMethodType = FindType(assemblies, "LiteNetLib.DeliveryMethod");
            InferNetworkApiTypes();
            pluginPresent = krokoshaScavType != null || netType != null;
            networkStateFrame = -1;
        }

        private static void InferNetworkApiTypes()
        {
            if (netDataReaderType == null && networkDelegateType != null)
            {
                MethodInfo method = networkDelegateType.GetMethod("Invoke");
                ParameterInfo[] parameters = method != null ? method.GetParameters() : null;
                if (parameters != null && parameters.Length == 2)
                {
                    netDataReaderType = UnwrapByRef(parameters[1].ParameterType);
                }
            }

            if (netDataWriterType == null && netType != null)
            {
                MethodInfo method = FindStaticMethod(netType, "CreateWriter", new Type[] { typeof(ushort) });
                netDataWriterType = method != null ? method.ReturnType : null;
            }

            if ((deliveryMethodType == null || netDataWriterType == null) && netType != null)
            {
                MethodInfo method = netType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(candidate => candidate.Name == "Server_SendToClients" && candidate.GetParameters().Length == 3);
                ParameterInfo[] parameters = method != null ? method.GetParameters() : null;
                if (parameters != null)
                {
                    deliveryMethodType = deliveryMethodType ?? UnwrapByRef(parameters[0].ParameterType);
                    netDataWriterType = netDataWriterType ?? UnwrapByRef(parameters[1].ParameterType);
                }
            }
        }

        private static Type UnwrapByRef(Type type)
        {
            return type != null && type.IsByRef ? type.GetElementType() : type;
        }

        private static Type FindType(Assembly[] assemblies, string fullName)
        {
            return assemblies.Select(delegate(Assembly assembly)
            {
                try
                {
                    return assembly.GetType(fullName, false);
                }
                catch
                {
                    return null;
                }
            }).FirstOrDefault(type => type != null);
        }

        private static Body GetBodyFromClientId(uint clientId)
        {
            EnsureResolved();
            if (netPlayerLookupMethod == null)
            {
                return null;
            }

            try
            {
                object[] parameters = new object[3];
                parameters[0] = clientId;
                object result = netPlayerLookupMethod.Invoke(null, parameters);
                return result is bool && (bool)result ? parameters[2] as Body : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool SendSimpleUintUintToServer(ushort msgId, uint data, uint data2, bool reliable)
        {
            return InvokeSendSimple(new Type[]
            {
                typeof(ushort),
                typeof(uint),
                typeof(uint),
                typeof(bool)
            }, new object[]
            {
                msgId,
                data,
                data2,
                reliable
            });
        }

        private static bool SendSimpleUintUintByteToServer(ushort msgId, uint data, uint data2, byte data3, bool reliable)
        {
            return InvokeSendSimple(new Type[]
            {
                typeof(ushort),
                typeof(uint),
                typeof(uint),
                typeof(byte),
                typeof(bool)
            }, new object[]
            {
                msgId,
                data,
                data2,
                data3,
                reliable
            });
        }

        private static bool InvokeSendSimple(Type[] signature, params object[] args)
        {
            EnsureResolved();
            MethodInfo methodInfo = FindStaticMethod(krokoshaScavType, "Client_SendSimpleMessageToServer", signature);
            if (methodInfo == null)
            {
                return false;
            }

            try
            {
                methodInfo.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static uint ReadUInt(object reader)
        {
            if (reader == null)
            {
                return 0U;
            }

            object[] parameters = new object[] { 0U };
            MethodInfo method = reader.GetType().GetMethod("Get", new Type[] { typeof(uint).MakeByRefType() });
            if (method != null)
            {
                method.Invoke(reader, parameters);
            }

            return parameters[0] is uint ? (uint)parameters[0] : 0U;
        }

        private static byte ReadByte(object reader)
        {
            if (reader == null)
            {
                return 0;
            }

            object[] parameters = new object[] { (byte)0 };
            MethodInfo method = reader.GetType().GetMethod("Get", new Type[] { typeof(byte).MakeByRefType() });
            if (method != null)
            {
                method.Invoke(reader, parameters);
            }

            return parameters[0] is byte ? (byte)parameters[0] : (byte)0;
        }

        private static bool ReadStaticBool(Type type, string name)
        {
            EnsureResolved();
            if (type == null)
            {
                return false;
            }

            try
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    object value = property.GetValue(null, null);
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }

                FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    object value = field.GetValue(null);
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool InvokeStaticBool(Type type, string name)
        {
            EnsureResolved();
            if (type == null)
            {
                return false;
            }

            try
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                object result = method != null ? method.Invoke(null, null) : null;
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static object InvokeStatic(Type type, string name, params object[] args)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo methodInfo = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(candidate => candidate.Name == name && ParametersMatch(candidate, args.Select(arg => arg != null ? arg.GetType() : null).ToArray()));
            return methodInfo != null ? methodInfo.Invoke(null, args) : null;
        }

        private static object InvokeInstance(object instance, string name, params object[] args)
        {
            if (instance == null)
            {
                return null;
            }

            MethodInfo methodInfo = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(candidate => candidate.Name == name && ParametersMatch(candidate, args.Select(arg => arg != null ? arg.GetType() : null).ToArray()));
            return methodInfo != null ? methodInfo.Invoke(instance, args) : null;
        }

        private static MethodInfo FindStaticMethod(Type type, string name, Type[] signature)
        {
            if (type == null)
            {
                return null;
            }

            return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(method => method.Name == name && ParametersMatch(method, signature));
        }

        private static bool ParametersMatch(MethodBase method, Type[] signature)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != signature.Length)
            {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType();
                }

                Type signatureType = signature[i];
                if (signatureType != null && !parameterType.IsAssignableFrom(signatureType))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddContainerStatusChanged(object syncInfo)
        {
            if (containerStatusChangedField == null || syncInfo == null)
            {
                return;
            }

            try
            {
                object value = containerStatusChangedField.GetValue(null);
                MethodInfo methodInfo = value != null ? value.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public) : null;
                if (methodInfo != null)
                {
                    methodInfo.Invoke(value, new object[] { syncInfo });
                }
            }
            catch
            {
            }
        }

        private static void LogWarning(string message)
        {
            if (Logger != null)
            {
                Logger.LogWarning("[MpCompat] " + message);
            }
        }

        private static bool warnedMissingSyncId;

        private static void WarnOnce(string message)
        {
            if (warnedMissingSyncId)
            {
                return;
            }

            warnedMissingSyncId = true;
            LogWarning(message);
        }

        private sealed class InventoryMutationScope : IDisposable
        {
            private readonly bool hadFlag;
            private readonly object previous;
            private bool disposed;

            public InventoryMutationScope(bool hadFlag, object previous)
            {
                this.hadFlag = hadFlag;
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                try
                {
                    if (hadFlag && inventoryActionFlagField != null)
                    {
                        inventoryActionFlagField.SetValue(null, previous);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            {
            }
        }
    }
}
