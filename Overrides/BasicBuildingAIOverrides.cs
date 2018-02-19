﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using ICities;
using Klyte.Extensions;
using Klyte.Harmony;
using Klyte.ServiceVehiclesManager.Extensors.VehicleExt;
using Klyte.ServiceVehiclesManager.Utils;
using Klyte.TransportLinesManager.Extensors;
using Klyte.TransportLinesManager.Extensors.TransportTypeExt;
using Klyte.TransportLinesManager.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Klyte.ServiceVehiclesManager.Overrides
{
    internal interface IBasicBuildingAIOverrides
    {
        string GetVehicleMaxCountField(VehicleInfo.VehicleType veh);
        Dictionary<TransferManager.TransferReason, Tuple<VehicleInfo.VehicleType, bool, bool>> GetManagedReasons(BuildingInfo info);
        bool AllowVehicleType(VehicleInfo.VehicleType type);
    }
    internal interface IBasicBuildingAIOverrides<U> : IBasicBuildingAIOverrides where U : PrefabAI
    {
        Dictionary<TransferManager.TransferReason, Tuple<VehicleInfo.VehicleType, bool, bool>> GetManagedReasons(U ai, TransferManager.TransferOffer offer);
    }

    internal abstract class BasicBuildingAIOverrides<T, U> : Redirector<T>, IBasicBuildingAIOverrides<U> where T : BasicBuildingAIOverrides<T, U>, new() where U : BuildingAI
    {
        #region Overrides
        protected static BasicBuildingAIOverrides<T, U> instance;

        public Dictionary<TransferManager.TransferReason, Tuple<VehicleInfo.VehicleType, bool, bool>> GetManagedReasons(BuildingInfo info)
        {
            return GetManagedReasons((U)info.GetAI(), default(TransferManager.TransferOffer));
        }
        public abstract Dictionary<TransferManager.TransferReason, Tuple<VehicleInfo.VehicleType, bool, bool>> GetManagedReasons(U ai, TransferManager.TransferOffer offer);
        public abstract string GetVehicleMaxCountField(VehicleInfo.VehicleType veh);
        public abstract bool AllowVehicleType(VehicleInfo.VehicleType type);


        public static bool StartTransferDepot(U __instance, ushort buildingID, ref Building data, TransferManager.TransferReason reason, TransferManager.TransferOffer offer)
        {
            return StartTransfer(__instance, buildingID, ref data, reason, offer);
        }

        public static bool StartTransfer(U __instance, ushort buildingID, ref Building data, TransferManager.TransferReason material, TransferManager.TransferOffer offer)
        {
            var managedReasons = instance?.GetManagedReasons(__instance, offer);
            if (!managedReasons?.Keys.Contains(material) ?? true)
            {
                return true;
            }

            SVMUtils.doLog("START TRANSFER: {0} , {1}", typeof(U), material);
            foreach (var tr in managedReasons)
            {
                if (instance.ProcessOffer(buildingID, data, material, offer, tr.Key, tr.Value, __instance))
                {
                    return false;
                }
            }
            SVMUtils.doLog("END TRANSFER: {0} , {1}", typeof(U), material);
            return true;
        }

        protected virtual bool ProcessOffer(ushort buildingID, Building data, TransferManager.TransferReason material, TransferManager.TransferOffer offer, TransferManager.TransferReason trTarget, Tuple<VehicleInfo.VehicleType, bool, bool> tup, U instance)
        {
            if (material == trTarget)
            {
                ServiceSystemDefinition def = ServiceSystemDefinition.from(instance.m_info, tup.First);
                if (def == null)
                {
                    SVMUtils.doLog("SSD Não definido para: {0} {1} {2} {3}", instance.m_info.m_class.m_service, instance.m_info.m_class.m_subService, instance.m_info.m_class.m_level, tup.First);
                    return false;
                }
                SVMUtils.doLog("[{1}] SSD = {0}", def, material);
                VehicleInfo randomVehicleInfo = ServiceSystemDefinition.availableDefinitions[def].GetAModel(buildingID);
                SVMUtils.doLog("[{1}] Veh = {0}", randomVehicleInfo?.ToString() ?? "<NULL>", material);
                if (randomVehicleInfo != null)
                {
                    Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;
                    instance.CalculateSpawnPosition(buildingID, ref data, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, out Vector3 position, out Vector3 vector2);
                    if (Singleton<VehicleManager>.instance.CreateVehicle(out ushort num, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, position, material, tup.Second, tup.Third))
                    {
                        randomVehicleInfo.m_vehicleAI.SetSource(num, ref vehicles.m_buffer[(int)num], buildingID);
                        randomVehicleInfo.m_vehicleAI.StartTransfer(num, ref vehicles.m_buffer[(int)num], material, offer);
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Hooking

        public override void Awake()
        {
            instance = this;
            var from = typeof(U).GetMethod("StartTransfer", allFlags);
            if (from == null)
            {
                if (typeof(DepotAI).IsAssignableFrom(typeof(U)))
                {
                    from = typeof(DepotAI).GetMethod("StartTransfer", allFlags);
                }
            }
            var to = typeof(BasicBuildingAIOverrides<T, U>).GetMethod(typeof(DepotAI).IsAssignableFrom(typeof(U)) ? "StartTransferDepot" : "StartTransfer", allFlags);
            SVMUtils.doLog("Loading Hooks: {0} ({1}=>{2})", typeof(U), from, to);
            AddRedirect(from, to);
        }


        #endregion
    }


    public class Tuple<T1, T2, T3>
    {
        public T1 First { get; private set; }
        public T2 Second { get; private set; }
        public T3 Third { get; private set; }
        internal Tuple(T1 first, T2 second, T3 third)
        {
            First = first;
            Second = second;
            Third = third;
        }
    }

    public static class Tuple
    {
        public static Tuple<T1, T2, T3> New<T1, T2, T3>(T1 first, T2 second, T3 third)
        {
            var tuple = new Tuple<T1, T2, T3>(first, second, third);
            return tuple;
        }
    }
}