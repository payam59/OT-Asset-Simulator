using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO.BACnet;
using OLRTLabSim.Models;
using OLRTLabSim.Data;

namespace OLRTLabSim.Services
{
    public class BacnetRuntimeManager
    {
        private readonly ConcurrentDictionary<long, BacnetClient> _bbmdLifecycles = new();
        private readonly ConcurrentDictionary<long, uint> _bbmdDeviceIds = new();
        private readonly ConcurrentDictionary<long, object> _bbmdStatus = new();
        private readonly ConcurrentDictionary<string, BacnetObject> _objectIndex = new();

        public bool Installed => true; // C# library loaded

        public class BacnetObject
        {
            public string Name { get; set; }
            public uint Instance { get; set; }
            public BacnetObjectTypes ObjectType { get; set; }
            public double PresentValue { get; set; }
            public long BbmdId { get; set; }
        }


        private async Task RebuildLocalDevice()
        {
            if (_bbmdLifecycles.ContainsKey(0))
                await StopBbmd(0);

            try
            {
                var transport = new BacnetIpUdpProtocolTransport(47808, false, false, 1476, "0.0.0.0");
                var client = new BacnetClient(transport);

                client.OnReadPropertyRequest += Client_OnReadPropertyRequest;
                client.OnWritePropertyRequest += Client_OnWritePropertyRequest;
                client.OnWhoIs += Client_OnWhoIs;
                client.OnReadPropertyMultipleRequest += Client_OnReadPropertyMultipleRequest;

                client.Start();

                _bbmdLifecycles[0] = client;
                _bbmdDeviceIds[0] = 1; // Default device ID for local device is 1
                _bbmdStatus[0] = new { running = true, message = $"Listening on UDP 0.0.0.0:47808 (Local)" };
            }
            catch (Exception ex)
            {
                _bbmdStatus[0] = new { running = false, message = $"Failed to start local device: {ex.Message}" };
            }
        }

        private async Task RebuildBbmdLifecycle(Bbmd bbmd)
        {
            if (_bbmdLifecycles.ContainsKey(bbmd.Id))
                await StopBbmd(bbmd.Id);

            if (bbmd.Enabled == 0) return;

            string ipAddress = string.IsNullOrWhiteSpace(bbmd.IpAddress) || bbmd.IpAddress == "0.0.0.0" ? "0.0.0.0" : bbmd.IpAddress;

            try
            {
                var transport = new BacnetIpUdpProtocolTransport((int)bbmd.Port, false, false, 1476, ipAddress);
                var client = new BacnetClient(transport);

                client.OnReadPropertyRequest += Client_OnReadPropertyRequest;
                client.OnWritePropertyRequest += Client_OnWritePropertyRequest;
                client.OnWhoIs += Client_OnWhoIs;
                client.OnReadPropertyMultipleRequest += Client_OnReadPropertyMultipleRequest;

                client.Start();

                _bbmdLifecycles[bbmd.Id] = client;
                _bbmdDeviceIds[bbmd.Id] = (uint)bbmd.DeviceId;
                _bbmdStatus[bbmd.Id] = new { running = true, message = $"Listening on UDP {ipAddress}:{bbmd.Port}" };
            }
            catch (SocketException ex)
            {
                _bbmdStatus[bbmd.Id] = new { running = false, message = $"Port {bbmd.Port} in use: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _bbmdStatus[bbmd.Id] = new { running = false, message = $"Failed to start: {ex.Message}" };
            }
        }


        private void Client_OnWhoIs(BacnetClient sender, BacnetAddress adr, int lowLimit, int highLimit)
        {
            var pair = _bbmdLifecycles.FirstOrDefault(x => x.Value == sender);
            // Default pair returns Key = 0, Value = null if not found. If Key=0, Value might be null.
            // Better to explicitly iterate or check the Value safely.
            if (pair.Value != null)
            {
                if (_bbmdDeviceIds.TryGetValue(pair.Key, out uint deviceId))
                {
                    if ((lowLimit == -1 && highLimit == -1) || (deviceId >= lowLimit && deviceId <= highLimit))
                    {
                        sender.Iam(deviceId, BacnetSegmentations.SEGMENTATION_BOTH);
                    }
                }
            }
        }

        private void Client_OnReadPropertyMultipleRequest(BacnetClient sender, BacnetAddress adr, byte invokeId, IList<BacnetReadAccessSpecification> properties, BacnetMaxSegments maxSegments)
        {
            // Force fallback to single ReadProperty by sending an abort or error
            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invokeId, BacnetErrorClasses.ERROR_CLASS_SERVICES, BacnetErrorCodes.ERROR_CODE_REJECT_UNRECOGNIZED_SERVICE);
        }

        private void Client_OnWritePropertyRequest(BacnetClient sender, BacnetAddress adr, byte invokeId, BacnetObjectId objectId, BacnetPropertyValue value, BacnetMaxSegments maxSegments)
        {
            // Simple mapping - find object by id
            var obj = _objectIndex.Values.FirstOrDefault(o => o.Instance == objectId.Instance && o.ObjectType == objectId.Type);

            if (obj != null && value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
            {
                if (value.value != null && value.value.Any())
                {
                    obj.PresentValue = Convert.ToDouble(value.value[0].Value);
                    sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invokeId);
                    return;
                }
            }

            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invokeId, BacnetErrorClasses.ERROR_CLASS_PROPERTY, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
        }

                private void Client_OnReadPropertyRequest(BacnetClient sender, BacnetAddress adr, byte invokeId, BacnetObjectId objectId, BacnetPropertyReference property, BacnetMaxSegments maxSegments)
        {
            if (objectId.Type == BacnetObjectTypes.OBJECT_DEVICE)
            {
                var pair = _bbmdLifecycles.FirstOrDefault(x => x.Value == sender);
                if (pair.Value != null && _bbmdDeviceIds.TryGetValue(pair.Key, out uint deviceId))
                {
                    if (objectId.Instance == deviceId || objectId.Instance == 4194303)
                    {
                        var vals = new List<BacnetValue>();
                        switch (property.propertyIdentifier)
                        {
                            case (uint)BacnetPropertyIds.PROP_OBJECT_IDENTIFIER:
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE, deviceId)));
                                break;
                            case (uint)BacnetPropertyIds.PROP_OBJECT_NAME:
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"BBMD_Simulator_{deviceId}"));
                                break;
                            case (uint)BacnetPropertyIds.PROP_OBJECT_TYPE:
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, (uint)BacnetObjectTypes.OBJECT_DEVICE));
                                break;
                            case (uint)BacnetPropertyIds.PROP_SYSTEM_STATUS:
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, (uint)BacnetDeviceStatus.OPERATIONAL));
                                break;
                            case (uint)BacnetPropertyIds.PROP_VENDOR_IDENTIFIER:
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 0u));
                                break;
                            case (uint)BacnetPropertyIds.PROP_PROTOCOL_SERVICES_SUPPORTED:
                                var services = new BacnetBitString();
                                services.SetBit(40, false);
                                services.SetBit((byte)BacnetServicesSupported.SERVICE_SUPPORTED_READ_PROPERTY, true);
                                services.SetBit((byte)BacnetServicesSupported.SERVICE_SUPPORTED_WRITE_PROPERTY, true);
                                services.SetBit((byte)BacnetServicesSupported.SERVICE_SUPPORTED_WHO_IS, true);
                                services.SetBit((byte)BacnetServicesSupported.SERVICE_SUPPORTED_I_AM, true);
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, services));
                                break;
                            case (uint)BacnetPropertyIds.PROP_PROTOCOL_OBJECT_TYPES_SUPPORTED:
                                var types = new BacnetBitString();
                                types.SetBit(60, false);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_ANALOG_VALUE, true);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_ANALOG_INPUT, true);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, true);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_BINARY_VALUE, true);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_BINARY_INPUT, true);
                                types.SetBit((byte)BacnetObjectTypes.OBJECT_BINARY_OUTPUT, true);
                                vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, types));
                                break;
                            case (uint)BacnetPropertyIds.PROP_OBJECT_LIST:
                                if (property.propertyArrayIndex == 0) // size of array
                                {
                                    long bbmdIdLocal1 = pair.Value != null ? pair.Key : -1; uint count = (uint)_objectIndex.Values.Count(o => o.BbmdId == bbmdIdLocal1) + 1;
                                    vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, count));
                                }
                                else if (property.propertyArrayIndex == 1) // first element is device itself
                                {
                                    vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE, deviceId)));
                                }
                                else if (property.propertyArrayIndex == uint.MaxValue) // all objects
                                {
                                    vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE, deviceId)));
                                    long bbmdIdLocal2 = pair.Value != null ? pair.Key : -1; foreach(var o in _objectIndex.Values.Where(o => o.BbmdId == bbmdIdLocal2))
                                    {
                                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(o.ObjectType, o.Instance)));
                                    }
                                }
                                else
                                {
                                    int index = (int)property.propertyArrayIndex - 2;
                                    long bbmdIdLocal3 = pair.Value != null ? pair.Key : -1; var objects = _objectIndex.Values.Where(o => o.BbmdId == bbmdIdLocal3).ToList();
                                    if (index >= 0 && index < objects.Count)
                                    {
                                        var o = objects[index];
                                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(o.ObjectType, o.Instance)));
                                    }
                                }
                                break;
                        }

                        if (vals.Count > 0)
                        {
                            sender.ReadPropertyResponse(adr, invokeId, sender.GetSegmentBuffer(maxSegments), objectId, property, vals);
                            return;
                        }
                    }
                }
            }

            var obj = _objectIndex.Values.FirstOrDefault(o => o.Instance == objectId.Instance && o.ObjectType == objectId.Type);

            if (obj != null)
            {
                var vals = new List<BacnetValue>();
                switch (property.propertyIdentifier)
                {
                    case (uint)BacnetPropertyIds.PROP_OBJECT_IDENTIFIER:
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, new BacnetObjectId(obj.ObjectType, obj.Instance)));
                        break;
                    case (uint)BacnetPropertyIds.PROP_OBJECT_NAME:
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, obj.Name));
                        break;
                    case (uint)BacnetPropertyIds.PROP_OBJECT_TYPE:
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, (uint)obj.ObjectType));
                        break;
                    case (uint)BacnetPropertyIds.PROP_PRESENT_VALUE:
                        if (obj.ObjectType == BacnetObjectTypes.OBJECT_BINARY_INPUT || obj.ObjectType == BacnetObjectTypes.OBJECT_BINARY_OUTPUT || obj.ObjectType == BacnetObjectTypes.OBJECT_BINARY_VALUE)
                        {
                            vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, (uint)(obj.PresentValue >= 0.5 ? 1 : 0)));
                        }
                        else
                        {
                            vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)obj.PresentValue));
                        }
                        break;
                    case (uint)BacnetPropertyIds.PROP_STATUS_FLAGS:
                        var status = new BacnetBitString();
                        status.SetBit(0, false); // in-alarm
                        status.SetBit(1, false); // fault
                        status.SetBit(2, false); // overridden
                        status.SetBit(3, false); // out-of-service
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, status));
                        break;
                    case (uint)BacnetPropertyIds.PROP_OUT_OF_SERVICE:
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, false));
                        break;
                    case (uint)BacnetPropertyIds.PROP_UNITS:
                        vals.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, 95u)); // 95 = no units
                        break;
                }

                if (vals.Count > 0)
                {
                    sender.ReadPropertyResponse(adr, invokeId, sender.GetSegmentBuffer(maxSegments), objectId, property, vals);
                    return;
                }
            }

            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invokeId, BacnetErrorClasses.ERROR_CLASS_PROPERTY, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
        }


        public async Task StartBbmd(long id)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM bbmd WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            Bbmd bbmd = null;
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    bbmd = new Bbmd
                    {
                        Id = Convert.ToInt64(reader["id"]),
                        Name = reader["name"].ToString(),
                        Description = reader["description"].ToString(),
                        Port = Convert.ToInt64(reader["port"]),
                        DeviceId = Convert.ToInt64(reader["device_id"]),
                        IpAddress = reader["ip_address"].ToString(),
                        Enabled = Convert.ToInt64(reader["enabled"])
                    };
                }
            }

            if (bbmd != null)
            {
                await RebuildBbmdLifecycle(bbmd);
            }
        }

        public async Task StopBbmd(long id)
        {
            if (_bbmdLifecycles.TryRemove(id, out var client))
            {
                client.Dispose();
                _bbmdStatus[id] = new { running = false, message = "Stopped" };
            }
        }

        public async Task RegisterAsset(Asset asset)
        {

            if (!asset.BbmdId.HasValue)
            {
                if (!_bbmdLifecycles.ContainsKey(0))
                {
                    await RebuildLocalDevice();
                }
            }


            long bbmdId = asset.BbmdId ?? 0;
            if (!_bbmdLifecycles.ContainsKey(bbmdId))
                return;

            string objTypeStr = (asset.ObjectType ?? "value").ToLower();
            BacnetObjectTypes objType = BacnetObjectTypes.OBJECT_ANALOG_VALUE;

            if (objTypeStr == "input")
            {
                objType = asset.SubType == "Digital" ? BacnetObjectTypes.OBJECT_BINARY_INPUT : BacnetObjectTypes.OBJECT_ANALOG_INPUT;
            }
            else if (objTypeStr == "output")
            {
                objType = asset.SubType == "Digital" ? BacnetObjectTypes.OBJECT_BINARY_OUTPUT : BacnetObjectTypes.OBJECT_ANALOG_OUTPUT;
            }
            else
            {
                objType = asset.SubType == "Digital" ? BacnetObjectTypes.OBJECT_BINARY_VALUE : BacnetObjectTypes.OBJECT_ANALOG_VALUE;
            }

            double presentVal = asset.CurrentValue;
            if (asset.SubType == "Digital")
            {
                bool isNormallyOpen = asset.IsNormallyOpen == 1;
                double simVal = asset.CurrentValue;
                if (!isNormallyOpen)
                    simVal = simVal >= 0.5 ? 0.0 : 1.0;
                presentVal = simVal >= 0.5 ? 1.0 : 0.0;
            }

            var bacnetObj = new BacnetObject
            {
                Name = asset.Name,
                Instance = (uint)asset.Address,
                ObjectType = objType,
                PresentValue = presentVal,
                BbmdId = bbmdId
            };

            _objectIndex[asset.Name] = bacnetObj;
        }

        public async Task UnregisterAsset(string name)
        {
            _objectIndex.TryRemove(name, out _);
        }

        public void UpdateValue(string name, double currentValue, string subType, long isNormallyOpen = 1)
        {
            if (!_objectIndex.TryGetValue(name, out var obj)) return;

            double presentVal = currentValue;
            if (subType == "Digital")
            {
                bool isNO = isNormallyOpen == 1;
                double simVal = currentValue;
                if (!isNO)
                    simVal = simVal >= 0.5 ? 0.0 : 1.0;
                presentVal = simVal >= 0.5 ? 1.0 : 0.0;
            }

            obj.PresentValue = presentVal;
        }

        public double? GetValue(string name)
        {
            if (_objectIndex.TryGetValue(name, out var obj))
            {
                return obj.PresentValue;
            }
            return null;
        }

        public async Task Bootstrap(List<Asset> assets, List<Bbmd> bbmds)
        {
            foreach (var bbmd in bbmds)
            {
                await StartBbmd(bbmd.Id);
            }

            foreach (var asset in assets)
            {
                if (asset.Protocol == "bacnet")
                {
                    await RegisterAsset(asset);
                }
            }
        }

        public async Task Shutdown()
        {
            foreach (var bbmd in _bbmdLifecycles.Keys.ToList())
            {
                await StopBbmd(bbmd);
            }
            _objectIndex.Clear();
        }

        public object Status()
        {
            return new
            {
                bacnet_installed = Installed,
                bbmd_status = _bbmdStatus,
                object_count = _objectIndex.Count
            };
        }
    }
}
