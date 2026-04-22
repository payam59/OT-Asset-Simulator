using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using OLRTLabSim.Models;
using OLRTLabSim.Data;
using OLRTLabSim.Services;
using OLRTLabSim.Helpers;
using System.Text.RegularExpressions;
using System.DirectoryServices.Protocols;
using System.Net;

namespace OLRTLabSim.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly BacnetRuntimeManager _bacnetManager;
        private readonly ModbusRuntimeManager _modbusManager;
        private readonly Dnp3RuntimeManager _dnp3Manager;

        public ApiController(BacnetRuntimeManager bacnetManager, ModbusRuntimeManager modbusManager, Dnp3RuntimeManager dnp3Manager)
        {
            _bacnetManager = bacnetManager;
            _modbusManager = modbusManager;
            _dnp3Manager = dnp3Manager;
        }

        private Asset? LoadAssetByName(string name)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM assets WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new Asset
            {
                Id = Convert.ToInt64(reader["id"]),
                Name = reader["name"].ToString(),
                Type = reader["type"].ToString(),
                SubType = reader["sub_type"].ToString(),
                Protocol = reader["protocol"].ToString(),
                Address = Convert.ToInt64(reader["address"]),
                MinRange = Convert.ToDouble(reader["min_range"]),
                MaxRange = Convert.ToDouble(reader["max_range"]),
                CurrentValue = Convert.ToDouble(reader["current_value"]),
                DriftRate = Convert.ToDouble(reader["drift_rate"]),
                ManualOverride = Convert.ToInt64(reader["manual_override"]),
                Icon = reader["icon"] == DBNull.Value ? null : reader["icon"].ToString(),
                Filename = reader["filename"] == DBNull.Value ? null : reader["filename"].ToString(),
                BacnetPort = Convert.ToInt64(reader["bacnet_port"]),
                BacnetDeviceId = Convert.ToInt64(reader["bacnet_device_id"]),
                IsNormallyOpen = Convert.ToInt64(reader["is_normally_open"]),
                ChangeProbability = Convert.ToDouble(reader["change_probability"]),
                ChangeInterval = Convert.ToInt64(reader["change_interval"]),
                LastFlipCheck = Convert.ToDouble(reader["last_flip_check"]),
                BbmdId = reader["bbmd_id"] == DBNull.Value ? null : Convert.ToInt64(reader["bbmd_id"]),
                ObjectType = reader["object_type"].ToString(),
                BacnetProperties = reader["bacnet_properties"] == DBNull.Value ? "{}" : reader["bacnet_properties"].ToString(),
                ModbusUnitId = Convert.ToInt64(reader["modbus_unit_id"]),
                ModbusRegisterType = reader["modbus_register_type"].ToString(),
                ModbusIp = reader["modbus_ip"] == DBNull.Value ? "0.0.0.0" : reader["modbus_ip"].ToString(),
                ModbusPort = Convert.ToInt64(reader["modbus_port"]),
                ModbusAlarmAddress = reader["modbus_alarm_address"] == DBNull.Value ? null : Convert.ToInt64(reader["modbus_alarm_address"]),
                ModbusAlarmBit = Convert.ToInt64(reader["modbus_alarm_bit"]),
                ModbusZeroBased = Convert.ToInt64(reader["modbus_zero_based"]),
                ModbusWordOrder = reader["modbus_word_order"].ToString(),
                Dnp3Ip = reader["dnp3_ip"] == DBNull.Value ? "0.0.0.0" : reader["dnp3_ip"].ToString(),
                Dnp3Port = Convert.ToInt64(reader["dnp3_port"]),
                Dnp3OutstationAddress = Convert.ToInt64(reader["dnp3_outstation_address"]),
                Dnp3MasterAddress = Convert.ToInt64(reader["dnp3_master_address"]),
                Dnp3PointClass = reader["dnp3_point_class"].ToString(),
                Dnp3EventClass = Convert.ToInt64(reader["dnp3_event_class"]),
                Dnp3StaticVariation = Convert.ToInt64(reader["dnp3_static_variation"]),
                AlarmState = Convert.ToInt64(reader["alarm_state"]),
                AlarmMessage = reader["alarm_message"] == DBNull.Value ? null : reader["alarm_message"].ToString()
            };
        }

        private void PushToRuntime(Asset asset)
        {
            if (asset.Protocol == "bacnet")
                _bacnetManager.UpdateValue(asset.Name, asset.CurrentValue, asset.SubType, asset.IsNormallyOpen);
            else if (asset.Protocol == "modbus")
                _modbusManager.WriteValue(asset);
            else if (asset.Protocol == "dnp3")
                _dnp3Manager.WriteValue(asset);
        }

        private static string? InferModbusRegisterTypeFromAddress(long address)
        {
            var token = Math.Abs(address).ToString();
            if (token.Length < 5) return null;
            return token[0] switch
            {
                '0' => "coil",
                '1' => "discrete",
                '3' => "input",
                '4' => "holding",
                _ => null
            };
        }

        [Authorize]
        [HttpGet("assets")]
        public IActionResult GetAssets()
        {
            var assets = new List<object>();
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM assets";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                assets.Add(new {
                    id = reader["id"],
                    name = reader["name"]?.ToString(),
                    type = reader["type"]?.ToString(),
                    sub_type = reader["sub_type"]?.ToString(),
                    protocol = reader["protocol"]?.ToString(),
                    address = reader["address"],
                    min_range = reader["min_range"],
                    max_range = reader["max_range"],
                    current_value = reader["current_value"],
                    drift_rate = reader["drift_rate"],
                    manual_override = reader["manual_override"],
                    icon = reader["icon"] == DBNull.Value ? null : reader["icon"]?.ToString(),
                    filename = reader["filename"] == DBNull.Value ? null : reader["filename"]?.ToString(),
                    bacnet_port = reader["bacnet_port"],
                    bacnet_device_id = reader["bacnet_device_id"],
                    is_normally_open = reader["is_normally_open"],
                    change_probability = reader["change_probability"],
                    change_interval = reader["change_interval"],
                    last_flip_check = reader["last_flip_check"],
                    bbmd_id = reader["bbmd_id"] == DBNull.Value ? null : reader["bbmd_id"],
                    object_type = reader["object_type"]?.ToString(),
                    bacnet_properties = reader["bacnet_properties"] == DBNull.Value ? "{}" : reader["bacnet_properties"]?.ToString(),
                    modbus_unit_id = reader["modbus_unit_id"],
                    modbus_register_type = reader["modbus_register_type"]?.ToString(),
                    modbus_ip = reader["modbus_ip"] == DBNull.Value ? null : reader["modbus_ip"]?.ToString(),
                    modbus_port = reader["modbus_port"],
                    modbus_alarm_address = reader["modbus_alarm_address"] == DBNull.Value ? null : reader["modbus_alarm_address"],
                    modbus_alarm_bit = reader["modbus_alarm_bit"],
                    modbus_zero_based = reader["modbus_zero_based"],
                    modbus_word_order = reader["modbus_word_order"]?.ToString(),
                    dnp3_ip = reader["dnp3_ip"] == DBNull.Value ? null : reader["dnp3_ip"]?.ToString(),
                    dnp3_port = reader["dnp3_port"],
                    dnp3_outstation_address = reader["dnp3_outstation_address"],
                    dnp3_master_address = reader["dnp3_master_address"],
                    dnp3_point_class = reader["dnp3_point_class"]?.ToString(),
                    dnp3_event_class = reader["dnp3_event_class"],
                    dnp3_static_variation = reader["dnp3_static_variation"],
                    alarm_state = reader["alarm_state"],
                    alarm_message = reader["alarm_message"] == DBNull.Value ? null : reader["alarm_message"]?.ToString(),
                    dnp3_kepware_address = _dnp3Manager.GetKepwareAddress(reader["name"]?.ToString() ?? "")
                });
            }
            return Ok(assets);
        }

        [Authorize]
        [HttpGet("assets/{name}")]
        public IActionResult GetAsset(string name)
        {
            var asset = LoadAssetByName(name);
            if (asset == null) return NotFound(new { detail = "Asset not found" });

            return Ok(new {
                id = asset.Id,
                name = asset.Name,
                type = asset.Type,
                sub_type = asset.SubType,
                protocol = asset.Protocol,
                address = asset.Address,
                min_range = asset.MinRange,
                max_range = asset.MaxRange,
                current_value = asset.CurrentValue,
                drift_rate = asset.DriftRate,
                manual_override = asset.ManualOverride,
                icon = asset.Icon,
                filename = asset.Filename,
                bacnet_port = asset.BacnetPort,
                bacnet_device_id = asset.BacnetDeviceId,
                is_normally_open = asset.IsNormallyOpen,
                change_probability = asset.ChangeProbability,
                change_interval = asset.ChangeInterval,
                bbmd_id = asset.BbmdId,
                object_type = asset.ObjectType,
                bacnet_properties = asset.BacnetProperties,
                modbus_unit_id = asset.ModbusUnitId,
                modbus_register_type = asset.ModbusRegisterType,
                modbus_ip = asset.ModbusIp,
                modbus_port = asset.ModbusPort,
                modbus_alarm_address = asset.ModbusAlarmAddress,
                modbus_alarm_bit = asset.ModbusAlarmBit,
                modbus_zero_based = asset.ModbusZeroBased,
                modbus_word_order = asset.ModbusWordOrder,
                dnp3_ip = asset.Dnp3Ip,
                dnp3_port = asset.Dnp3Port,
                dnp3_outstation_address = asset.Dnp3OutstationAddress,
                dnp3_master_address = asset.Dnp3MasterAddress,
                dnp3_point_class = asset.Dnp3PointClass,
                dnp3_event_class = asset.Dnp3EventClass,
                dnp3_static_variation = asset.Dnp3StaticVariation,
                alarm_state = asset.AlarmState,
                alarm_message = asset.AlarmMessage,
                dnp3_kepware_address = _dnp3Manager.GetKepwareAddress(asset.Name)
            });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPut("override/{name}")]
        public IActionResult OverrideAsset(string name, [FromQuery] double value)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE assets SET current_value = @value, manual_override = 1 WHERE name = @name";
            cmd.Parameters.AddWithValue("@value", value);
            cmd.Parameters.AddWithValue("@name", name);
            if (cmd.ExecuteNonQuery() == 0)
                return NotFound(new { detail = "Asset not found" });

            var asset = LoadAssetByName(name);
            if (asset != null)
            {
                PushToRuntime(asset);
            }

            Database.LogAudit("ASSET_OVERRIDE", User?.Identity?.Name ?? "Unknown", $"Manually overrode asset {name} to {value}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = $"{name} manually overridden to {value}" });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPut("release/{name}")]
        public IActionResult ReleaseAsset(string name)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE assets SET manual_override = 0 WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            if (cmd.ExecuteNonQuery() == 0)
                return NotFound(new { detail = "Asset not found" });

            Database.LogAudit("ASSET_RELEASE", User?.Identity?.Name ?? "Unknown", $"Released asset {name} to automation", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = $"{name} released to automation" });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPost("assets")]
        public async Task<IActionResult> CreateAsset([FromBody] Asset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.Name))
                return BadRequest(new { detail = "Asset name is required" });

            if ((asset.Protocol ?? "").Equals("modbus", StringComparison.OrdinalIgnoreCase))
            {
                var inferred = InferModbusRegisterTypeFromAddress(asset.Address);
                if (!string.IsNullOrWhiteSpace(inferred))
                {
                    var configured = (asset.ModbusRegisterType ?? "").Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(configured) && configured != inferred)
                    {
                        return BadRequest(new
                        {
                            detail = $"Address {asset.Address} maps to Modbus '{inferred}' table. Register type cannot be '{configured}'."
                        });
                    }
                    asset.ModbusRegisterType = inferred;
                }
            }

            asset.Name = asset.Name.Trim().Replace(" ", "_");

            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM assets WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", asset.Name);
            if (Convert.ToInt64(cmd.ExecuteScalar()) > 0)
                return BadRequest(new { detail = "Asset with this name already exists" });

            string normType = (asset.Protocol == "bacnet" ? asset.ObjectType : "value");

            cmd.CommandText = @"
                INSERT INTO assets (
                    name, type, sub_type, protocol, address, min_range, max_range,
                    current_value, drift_rate, icon, filename, bacnet_port,
                    bacnet_device_id, is_normally_open, change_probability,
                    change_interval, last_flip_check, bbmd_id, object_type, bacnet_properties,
                    modbus_unit_id, modbus_register_type, modbus_ip, modbus_port,
                    modbus_alarm_address, modbus_alarm_bit,
                    dnp3_ip, dnp3_port, dnp3_outstation_address, dnp3_master_address,
                    dnp3_point_class, dnp3_event_class, dnp3_static_variation, alarm_state
                )
                VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, @p19, @p20, @p21, @p22, @p23, @p24, @p25, @p26, @p27, @p28, @p29, @p30, @p31, @p32, @p33, 0)
            ";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p1", asset.Name);
            cmd.Parameters.AddWithValue("@p2", (object)(asset.Type ?? "General"));
            cmd.Parameters.AddWithValue("@p3", (object)(asset.SubType ?? "Analog"));
            cmd.Parameters.AddWithValue("@p4", (object)(asset.Protocol ?? "bacnet"));
            cmd.Parameters.AddWithValue("@p5", asset.Address);
            cmd.Parameters.AddWithValue("@p6", asset.MinRange);
            cmd.Parameters.AddWithValue("@p7", asset.MaxRange);
            cmd.Parameters.AddWithValue("@p8", asset.CurrentValue);
            cmd.Parameters.AddWithValue("@p9", asset.DriftRate);
            cmd.Parameters.AddWithValue("@p10", asset.Icon ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p11", asset.Filename ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p12", asset.BacnetPort > 0 ? asset.BacnetPort : 47808);
            cmd.Parameters.AddWithValue("@p13", asset.BacnetDeviceId > 0 ? asset.BacnetDeviceId : 1234);
            cmd.Parameters.AddWithValue("@p14", asset.IsNormallyOpen);
            cmd.Parameters.AddWithValue("@p15", asset.ChangeProbability);
            cmd.Parameters.AddWithValue("@p16", asset.ChangeInterval > 0 ? asset.ChangeInterval : 15);
            cmd.Parameters.AddWithValue("@p17", Database.GetCurrentUnixTime());
            cmd.Parameters.AddWithValue("@p18", asset.BbmdId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p19", (object)(normType ?? "value"));
            cmd.Parameters.AddWithValue("@p20", string.IsNullOrWhiteSpace(asset.BacnetProperties) ? "{}" : asset.BacnetProperties);
            cmd.Parameters.AddWithValue("@p21", asset.ModbusUnitId > 0 ? asset.ModbusUnitId : 1);
            cmd.Parameters.AddWithValue("@p22", (object)(asset.ModbusRegisterType ?? "holding"));
            cmd.Parameters.AddWithValue("@p23", (object)(asset.ModbusIp ?? "0.0.0.0"));
            cmd.Parameters.AddWithValue("@p24", asset.ModbusPort > 0 ? asset.ModbusPort : 5020);
            cmd.Parameters.AddWithValue("@p25", asset.ModbusAlarmAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p26", asset.ModbusAlarmBit);
            cmd.Parameters.AddWithValue("@p27", (object)(asset.Dnp3Ip ?? "0.0.0.0"));
            cmd.Parameters.AddWithValue("@p28", asset.Dnp3Port > 0 ? asset.Dnp3Port : 20000);
            cmd.Parameters.AddWithValue("@p29", asset.Dnp3OutstationAddress > 0 ? asset.Dnp3OutstationAddress : 10);
            cmd.Parameters.AddWithValue("@p30", asset.Dnp3MasterAddress > 0 ? asset.Dnp3MasterAddress : 1);
            cmd.Parameters.AddWithValue("@p31", (object)(asset.Dnp3PointClass ?? "analog_output"));
            cmd.Parameters.AddWithValue("@p32", asset.Dnp3EventClass > 0 ? asset.Dnp3EventClass : 1);
            cmd.Parameters.AddWithValue("@p33", asset.Dnp3StaticVariation);

            cmd.ExecuteNonQuery();

            if (asset.Protocol == "bacnet")
                await _bacnetManager.RegisterAsset(asset);
            else if (asset.Protocol == "modbus")
                await _modbusManager.RegisterAsset(asset);
            else if (asset.Protocol == "dnp3")
                await _dnp3Manager.RegisterAsset(asset);

            PushToRuntime(asset);
            Database.LogAudit("ASSET_CREATE", User?.Identity?.Name ?? "Unknown", $"Created asset {asset.Name}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = "Asset added successfully" });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPut("assets/{name}")]
        public async Task<IActionResult> UpdateAsset(string name, [FromBody] Asset asset)
        {
            var existing = LoadAssetByName(name);
            if (existing == null)
                return NotFound(new { detail = "Asset not found" });

            if ((asset.Protocol ?? "").Equals("modbus", StringComparison.OrdinalIgnoreCase))
            {
                var inferred = InferModbusRegisterTypeFromAddress(asset.Address);
                if (!string.IsNullOrWhiteSpace(inferred))
                {
                    var configured = (asset.ModbusRegisterType ?? "").Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(configured) && configured != inferred)
                    {
                        return BadRequest(new
                        {
                            detail = $"Address {asset.Address} maps to Modbus '{inferred}' table. Register type cannot be '{configured}'."
                        });
                    }
                    asset.ModbusRegisterType = inferred;
                }
            }

            if (existing.Protocol == "bacnet")
                await _bacnetManager.UnregisterAsset(name);
            else if (existing.Protocol == "modbus")
                await _modbusManager.UnregisterAsset(name);
            else if (existing.Protocol == "dnp3")
                await _dnp3Manager.UnregisterAsset(name);

            var resolvedDnp3Ip = string.IsNullOrWhiteSpace(asset.Dnp3Ip) ? existing.Dnp3Ip : asset.Dnp3Ip;
            var resolvedDnp3Port = asset.Dnp3Port > 0 ? asset.Dnp3Port : existing.Dnp3Port;
            var resolvedDnp3OutstationAddress = asset.Dnp3OutstationAddress > 0 ? asset.Dnp3OutstationAddress : existing.Dnp3OutstationAddress;
            var resolvedDnp3MasterAddress = asset.Dnp3MasterAddress > 0 ? asset.Dnp3MasterAddress : existing.Dnp3MasterAddress;
            var resolvedDnp3PointClass = string.IsNullOrWhiteSpace(asset.Dnp3PointClass) ? existing.Dnp3PointClass : asset.Dnp3PointClass;
            var resolvedDnp3EventClass = asset.Dnp3EventClass > 0 ? asset.Dnp3EventClass : existing.Dnp3EventClass;
            var resolvedDnp3StaticVariation = asset.Dnp3StaticVariation >= 0 ? asset.Dnp3StaticVariation : existing.Dnp3StaticVariation;

            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            string normType = (asset.Protocol == "bacnet" ? asset.ObjectType : "value");

            cmd.CommandText = @"
                UPDATE assets
                SET type = @p2, sub_type = @p3, protocol = @p4, address = @p5, min_range = @p6,
                    max_range = @p7, drift_rate = @p9, icon = @p10, filename = @p11, bacnet_port = @p12,
                    bacnet_device_id = @p13, is_normally_open = @p14, change_probability = @p15,
                    change_interval = @p16, bbmd_id = @p18, object_type = @p19, modbus_unit_id = @p21,
                    bacnet_properties = @p20, modbus_register_type = @p22, modbus_ip = @p23, modbus_port = @p24,
                    modbus_alarm_address = @p25, modbus_alarm_bit = @p26,
                    dnp3_ip = @p27, dnp3_port = @p28, dnp3_outstation_address = @p29, dnp3_master_address = @p30,
                    dnp3_point_class = @p31, dnp3_event_class = @p32, dnp3_static_variation = @p33
                WHERE name = @p1
            ";

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p1", name);
            cmd.Parameters.AddWithValue("@p2", (object)(asset.Type ?? "General"));
            cmd.Parameters.AddWithValue("@p3", (object)(asset.SubType ?? "Analog"));
            cmd.Parameters.AddWithValue("@p4", (object)(asset.Protocol ?? "bacnet"));
            cmd.Parameters.AddWithValue("@p5", asset.Address);
            cmd.Parameters.AddWithValue("@p6", asset.MinRange);
            cmd.Parameters.AddWithValue("@p7", asset.MaxRange);
            cmd.Parameters.AddWithValue("@p9", asset.DriftRate);
            cmd.Parameters.AddWithValue("@p10", asset.Icon ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p11", asset.Filename ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p12", asset.BacnetPort > 0 ? asset.BacnetPort : 47808);
            cmd.Parameters.AddWithValue("@p13", asset.BacnetDeviceId > 0 ? asset.BacnetDeviceId : 1234);
            cmd.Parameters.AddWithValue("@p14", asset.IsNormallyOpen);
            cmd.Parameters.AddWithValue("@p15", asset.ChangeProbability);
            cmd.Parameters.AddWithValue("@p16", asset.ChangeInterval > 0 ? asset.ChangeInterval : 15);
            cmd.Parameters.AddWithValue("@p18", asset.BbmdId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p19", (object)(normType ?? "value"));
            cmd.Parameters.AddWithValue("@p20", string.IsNullOrWhiteSpace(asset.BacnetProperties) ? "{}" : asset.BacnetProperties);
            cmd.Parameters.AddWithValue("@p21", asset.ModbusUnitId > 0 ? asset.ModbusUnitId : 1);
            cmd.Parameters.AddWithValue("@p22", (object)(asset.ModbusRegisterType ?? "holding"));
            cmd.Parameters.AddWithValue("@p23", (object)(asset.ModbusIp ?? "0.0.0.0"));
            cmd.Parameters.AddWithValue("@p24", asset.ModbusPort > 0 ? asset.ModbusPort : 5020);
            cmd.Parameters.AddWithValue("@p25", asset.ModbusAlarmAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p26", asset.ModbusAlarmBit);
            cmd.Parameters.AddWithValue("@p27", (object)(resolvedDnp3Ip ?? "0.0.0.0"));
            cmd.Parameters.AddWithValue("@p28", resolvedDnp3Port > 0 ? resolvedDnp3Port : 20000);
            cmd.Parameters.AddWithValue("@p29", resolvedDnp3OutstationAddress > 0 ? resolvedDnp3OutstationAddress : 10);
            cmd.Parameters.AddWithValue("@p30", resolvedDnp3MasterAddress > 0 ? resolvedDnp3MasterAddress : 1);
            cmd.Parameters.AddWithValue("@p31", (object)(resolvedDnp3PointClass ?? "analog_output"));
            cmd.Parameters.AddWithValue("@p32", resolvedDnp3EventClass > 0 ? resolvedDnp3EventClass : 1);
            cmd.Parameters.AddWithValue("@p33", resolvedDnp3StaticVariation);

            cmd.ExecuteNonQuery();

            if (asset.Protocol == "bacnet")
                await _bacnetManager.RegisterAsset(asset);
            else if (asset.Protocol == "modbus")
                await _modbusManager.RegisterAsset(asset);
            else if (asset.Protocol == "dnp3")
                await _dnp3Manager.RegisterAsset(asset);

            var updated = LoadAssetByName(name);
            if (updated != null) PushToRuntime(updated);

            Database.LogAudit("ASSET_UPDATE", User?.Identity?.Name ?? "Unknown", $"Updated asset {name}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = "Asset updated successfully" });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpDelete("assets/{name}")]
        public async Task<IActionResult> DeleteAsset(string name)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT protocol FROM assets WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            var protoObj = cmd.ExecuteScalar();
            if (protoObj == null)
                return NotFound(new { detail = "Asset not found" });

            string protocol = protoObj.ToString();

            if (protocol == "bacnet")
                await _bacnetManager.UnregisterAsset(name);
            else if (protocol == "modbus")
                await _modbusManager.UnregisterAsset(name);
            else if (protocol == "dnp3")
                await _dnp3Manager.UnregisterAsset(name);

            cmd.CommandText = "DELETE FROM assets WHERE name = @name";
            cmd.ExecuteNonQuery();

            Database.LogAudit("ASSET_DELETE", User?.Identity?.Name ?? "Unknown", $"Deleted asset {name}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = "Asset deleted" });
        }

        [Authorize]
        [HttpGet("bbmd")]
        public IActionResult GetBbmds()
        {
            var bbmds = new List<object>();
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM bbmd";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bbmds.Add(new {
                    id = reader["id"],
                    name = reader["name"]?.ToString(),
                    description = reader["description"] == DBNull.Value ? null : reader["description"]?.ToString(),
                    port = reader["port"],
                    device_id = reader["device_id"],
                    ip_address = reader["ip_address"] == DBNull.Value ? null : reader["ip_address"]?.ToString(),
                    enabled = reader["enabled"],
                    created_at = reader["created_at"]
                });
            }
            return Ok(bbmds);
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPost("bbmd")]
        public async Task<IActionResult> CreateBbmd([FromBody] Bbmd bbmd)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "INSERT INTO bbmd (name, description, port, device_id, ip_address, enabled, created_at) VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7)";
            cmd.Parameters.AddWithValue("@p1", bbmd.Name ?? (object)"Unknown");
            cmd.Parameters.AddWithValue("@p2", bbmd.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p3", bbmd.Port);
            cmd.Parameters.AddWithValue("@p4", bbmd.DeviceId);
            cmd.Parameters.AddWithValue("@p5", bbmd.IpAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p6", bbmd.Enabled);
            cmd.Parameters.AddWithValue("@p7", Database.GetCurrentUnixTime());

            try
            {
                cmd.ExecuteNonQuery();

                cmd.CommandText = "SELECT last_insert_rowid()";
                long id = (long)cmd.ExecuteScalar();

                await _bacnetManager.StartBbmd(id);

                return Ok(new { message = "BBMD definition created successfully", id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpPut("bbmd/{id}")]
        public async Task<IActionResult> UpdateBbmd(long id, [FromBody] Bbmd bbmd)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "UPDATE bbmd SET name = @p1, description = @p2, port = @p3, device_id = @p4, ip_address = @p5, enabled = @p6 WHERE id = @id";
            cmd.Parameters.AddWithValue("@p1", bbmd.Name ?? (object)"Unknown");
            cmd.Parameters.AddWithValue("@p2", bbmd.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p3", bbmd.Port);
            cmd.Parameters.AddWithValue("@p4", bbmd.DeviceId);
            cmd.Parameters.AddWithValue("@p5", bbmd.IpAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p6", bbmd.Enabled);
            cmd.Parameters.AddWithValue("@id", id);

            if (cmd.ExecuteNonQuery() == 0)
                return NotFound(new { detail = "BBMD not found" });

            await _bacnetManager.StopBbmd(id);
            if (bbmd.Enabled == 1)
            {
                await _bacnetManager.StartBbmd(id);
            }

            return Ok(new { message = "BBMD updated successfully" });
        }

        [Authorize(Roles = "admin,read_write")]
        [HttpDelete("bbmd/{id}")]
        public async Task<IActionResult> DeleteBbmd(long id)
        {
            await _bacnetManager.StopBbmd(id);

            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM bbmd WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return Ok(new { message = "BBMD deleted successfully" });
        }

        [Authorize]
        [HttpGet("alarms")]
        public IActionResult GetAlarms([FromQuery] int active_only = 0)
        {
            var alarms = new List<object>();
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = active_only == 1
                ? "SELECT * FROM alarm_events WHERE active = 1 ORDER BY created_at DESC LIMIT 100"
                : "SELECT * FROM alarm_events ORDER BY created_at DESC LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                alarms.Add(new {
                    id = reader["id"],
                    asset_id = reader["asset_id"],
                    asset_name = reader["asset_name"],
                    message = reader["message"],
                    active = reader["active"],
                    created_at = reader["created_at"],
                    cleared_at = reader["cleared_at"]
                });
            }
            return Ok(alarms);
        }

        [Authorize]
        [HttpGet("bacnet/status")]
        public IActionResult GetBacnetStatus() => Ok(_bacnetManager.Status());

        [Authorize]
        [HttpGet("modbus/status")]
        public IActionResult GetModbusStatus() => Ok(_modbusManager.Status());

        [Authorize]
        [HttpGet("dnp3/status")]
        public IActionResult GetDnp3Status() => Ok(_dnp3Manager.Status());

        [Authorize(Roles = "admin")]
        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] string type, [FromQuery] string search = "", [FromQuery] double? startDate = null, [FromQuery] double? endDate = null)
        {
            if (type != "audit" && type != "events")
            {
                return BadRequest(new { error = "Invalid log type specified." });
            }

            using var conn = type == "audit" ? Database.GetAuditConnection() : Database.GetEventsConnection();
            using var cmd = conn.CreateCommand();

            var conditions = new List<string>();
            if (startDate.HasValue)
            {
                conditions.Add("timestamp >= @startDate");
                cmd.Parameters.AddWithValue("@startDate", startDate.Value);
            }
            if (endDate.HasValue)
            {
                conditions.Add("timestamp <= @endDate");
                cmd.Parameters.AddWithValue("@endDate", endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (type == "audit")
                {
                    conditions.Add("(action LIKE @search OR username LIKE @search OR details LIKE @search OR ip_address LIKE @search)");
                }
                else
                {
                    conditions.Add("(asset_name LIKE @search OR status LIKE @search OR reason LIKE @search OR details LIKE @search)");
                }
                cmd.Parameters.AddWithValue("@search", $"%{search}%");
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            var tableName = type == "audit" ? "audit_logs" : "events_logs";
            cmd.CommandText = $"SELECT * FROM {tableName} {whereClause} ORDER BY timestamp DESC";

            var results = new List<Dictionary<string, object>>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            return Ok(results);
        }
    }

    [ApiController]
    [Route("api/auth")]

    public class AuthController : ControllerBase
    {
        private LdapConnection CreateLdapConnection(string adServer)
        {
            string host = adServer;
            bool useSsl = false;
            if (adServer.StartsWith("ldaps://", StringComparison.OrdinalIgnoreCase))
            {
                useSsl = true;
                host = adServer.Substring(8);
            }
            else if (adServer.StartsWith("ldap://", StringComparison.OrdinalIgnoreCase))
            {
                host = adServer.Substring(7);
            }

            var ldap = new LdapConnection(new LdapDirectoryIdentifier(host));
            ldap.SessionOptions.ProtocolVersion = 3;
            if (useSsl)
            {
                ldap.SessionOptions.SecureSocketLayer = true;
                ldap.SessionOptions.VerifyServerCertificate = (conn, cert) => true;
            }
            return ldap;
        }

        [Authorize(Roles = "admin")]
        [HttpPost("test-ad")]
        public IActionResult TestAdConnection([FromBody] SettingsModel req)
        {
            try
            {
                using var ldap = CreateLdapConnection(req.AdServer);
                string fqdn = req.AdServiceUser.Contains("@") ? req.AdServiceUser : $"{req.AdServiceUser}@{req.AdDomain}";
                ldap.Credential = new NetworkCredential(fqdn, req.AdServicePassword);
                ldap.AuthType = AuthType.Basic;
                ldap.Bind();
                return Ok(new { success = true, message = "Successfully connected and bound to AD." });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message + (ex.InnerException != null ? " -> " + ex.InnerException.Message : "") });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            using var conn = Database.GetConnection();
            using var settingsCmd = conn.CreateCommand();
            settingsCmd.CommandText = "SELECT * FROM settings WHERE id = 1";
            long sessionTimeout = 60;
            bool adEnabled = false;
            string adServer = "", adDomain = "", adGroupAdmin = "", adGroupRw = "", adGroupRo = "";
            using (var sReader = settingsCmd.ExecuteReader())
            {
                if (sReader.Read())
                {
                    sessionTimeout = sReader.GetInt64(sReader.GetOrdinal("session_timeout_minutes"));
                    adEnabled = sReader.GetInt64(sReader.GetOrdinal("ad_enabled")) == 1;
                    adServer = sReader.GetString(sReader.GetOrdinal("ad_server"));
                    adDomain = sReader.GetString(sReader.GetOrdinal("ad_domain"));
                    adGroupAdmin = sReader.GetString(sReader.GetOrdinal("ad_group_admin"));
                    adGroupRw = sReader.GetString(sReader.GetOrdinal("ad_group_rw"));
                    adGroupRo = sReader.GetString(sReader.GetOrdinal("ad_group_ro"));
                }
            }

            string accessLevel = null;
            long needsChange = 0;
            long userId = 0;

            if (adEnabled && !string.IsNullOrWhiteSpace(adServer))
            {
                try
                {
                    string fqdn = req.Username.Contains("@") ? req.Username : $"{req.Username}@{adDomain}";
                    using var ldap = CreateLdapConnection(adServer);
                    ldap.Credential = new NetworkCredential(fqdn, req.Password);
                    ldap.AuthType = AuthType.Basic;
                    ldap.Bind();

                    // Search for user's memberOf attribute
                    string searchFilter = $"(&(objectClass=user)(sAMAccountName={req.Username}))";
                    var searchRequest = new SearchRequest(
                        adDomain.Replace(".", ",DC=").Insert(0, "DC="), // Simple heuristic for base DN
                        searchFilter,
                        System.DirectoryServices.Protocols.SearchScope.Subtree,
                        "memberOf"
                    );

                    accessLevel = "read_only"; // Default AD role

                    try
                    {
                        var searchResponse = (SearchResponse)ldap.SendRequest(searchRequest);
                        if (searchResponse.Entries.Count > 0)
                        {
                            var entry = searchResponse.Entries[0];
                            if (entry.Attributes.Contains("memberOf"))
                            {
                                var memberOfAttr = entry.Attributes["memberOf"];
                                bool isAdmin = false;
                                bool isRw = false;
                                bool isRo = false;

                                foreach (var attrValue in memberOfAttr.GetValues(typeof(string)))
                                {
                                    string groupDn = attrValue.ToString();
                                    if (!string.IsNullOrWhiteSpace(adGroupAdmin) && groupDn.Contains(adGroupAdmin, StringComparison.OrdinalIgnoreCase)) isAdmin = true;
                                    if (!string.IsNullOrWhiteSpace(adGroupRw) && groupDn.Contains(adGroupRw, StringComparison.OrdinalIgnoreCase)) isRw = true;
                                    if (!string.IsNullOrWhiteSpace(adGroupRo) && groupDn.Contains(adGroupRo, StringComparison.OrdinalIgnoreCase)) isRo = true;
                                }

                                if (isAdmin) accessLevel = "admin";
                                else if (isRw) accessLevel = "read_write";
                                else if (isRo) accessLevel = "read_only";
                            }
                        }
                    }
                    catch
                    {
                        // Ignore search errors, fallback to default role or generic handling
                    }

                    userId = -1; // Fake ID for AD users
                    needsChange = 0; // AD manages its own passwords
                }
                catch
                {
                    // AD auth failed, try local fallback if allowed
                }
            }

            if (accessLevel == null)
            {
                // Local DB Auth
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, username, password, access_level, needs_password_change FROM users WHERE username = @username";
                cmd.Parameters.AddWithValue("@username", CryptoHelper.EncryptDeterministic(req.Username));

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string encPassword = reader.GetString(2);
                    string plainPassword = CryptoHelper.DecryptRandom(encPassword);

                    if (plainPassword == req.Password)
                    {
                        accessLevel = CryptoHelper.DecryptDeterministic(reader.GetString(3));
                        needsChange = reader.GetInt64(4);
                        userId = reader.GetInt64(0);
                    }
                }
            }

            if (accessLevel != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, req.Username),
                    new Claim(ClaimTypes.Role, accessLevel),
                    new Claim("NeedsPasswordChange", needsChange.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(sessionTimeout),
                    IsPersistent = true
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                Database.LogAudit("USER_LOGIN", req.Username, "User logged in", HttpContext.Connection.RemoteIpAddress?.ToString());
                return Ok(new { success = true, role = accessLevel, needs_password_change = needsChange == 1 });
            }

            return Unauthorized(new { error = "Invalid username or password" });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Database.LogAudit("USER_LOGOUT", User?.Identity?.Name ?? "Unknown", $"User logged out", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || userId == "-1") return Unauthorized();

            using var conn = Database.GetConnection();

            // Get settings
            using var settingsCmd = conn.CreateCommand();
            settingsCmd.CommandText = "SELECT password_complexity_regex, password_history_count FROM settings WHERE id = 1";
            string regexPattern = @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$!%*#?&]{8,}$";
            long historyCount = 3;
            using (var sReader = settingsCmd.ExecuteReader())
            {
                if (sReader.Read())
                {
                    regexPattern = sReader.GetString(0);
                    historyCount = sReader.GetInt64(1);
                }
            }

            if (!Regex.IsMatch(req.NewPassword, regexPattern))
            {
                return BadRequest(new { error = "Password does not meet complexity requirements." });
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT password FROM users WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", userId);

            var encPassword = (string?)cmd.ExecuteScalar();
            if (encPassword == null) return NotFound();

            var plainPassword = CryptoHelper.DecryptRandom(encPassword);
            if (plainPassword != req.OldPassword)
            {
                return BadRequest(new { error = "Invalid old password" });
            }

            if (plainPassword == req.NewPassword)
            {
                return BadRequest(new { error = "Password must be different from current password." });
            }

            // Check history
            using var histCmd = conn.CreateCommand();
            histCmd.CommandText = "SELECT password FROM password_history WHERE user_id = @id ORDER BY changed_at DESC LIMIT @limit";
            histCmd.Parameters.AddWithValue("@id", userId);
            histCmd.Parameters.AddWithValue("@limit", historyCount);
            using (var hReader = histCmd.ExecuteReader())
            {
                while (hReader.Read())
                {
                    var oldEnc = hReader.GetString(0);
                    if (CryptoHelper.DecryptRandom(oldEnc) == req.NewPassword)
                    {
                        return BadRequest(new { error = "Password was used recently." });
                    }
                }
            }

            // Save to history
            using var insertHistCmd = conn.CreateCommand();
            insertHistCmd.CommandText = "INSERT INTO password_history (user_id, password, changed_at) VALUES (@uid, @pass, @ts)";
            insertHistCmd.Parameters.AddWithValue("@uid", userId);
            insertHistCmd.Parameters.AddWithValue("@pass", encPassword);
            insertHistCmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            insertHistCmd.ExecuteNonQuery();

            // Update user
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE users SET password = @newpass, needs_password_change = 0 WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@newpass", CryptoHelper.EncryptRandom(req.NewPassword));
            updateCmd.Parameters.AddWithValue("@id", userId);
            updateCmd.ExecuteNonQuery();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Database.LogAudit("USER_LOGOUT", User?.Identity?.Name ?? "Unknown", $"User logged out", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true });
        }

        [Authorize]
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetMe()
        {
            return Ok(new {
                username = User.Identity?.Name,
                role = User.FindFirstValue(ClaimTypes.Role),
                needs_password_change = User.HasClaim("NeedsPasswordChange", "1")
            });
        }

        [Authorize(Roles = "admin")]
        [Authorize]
        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            var users = new List<object>();
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, access_level, needs_password_change FROM users";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new {
                    id = reader.GetInt64(0),
                    username = CryptoHelper.DecryptDeterministic(reader.GetString(1)),
                    access_level = CryptoHelper.DecryptDeterministic(reader.GetString(2)),
                    needs_password_change = reader.GetInt64(3) == 1
                });
            }
            return Ok(users);
        }

        [Authorize(Roles = "admin")]
        [Authorize(Roles = "admin,read_write")]
        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] UserCreateRequest req)
        {
            using var conn = Database.GetConnection();
            using var settingsCmd = conn.CreateCommand();
            settingsCmd.CommandText = "SELECT password_complexity_regex FROM settings WHERE id = 1";
            string regexPattern = @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$!%*#?&]{8,}$";
            using (var sReader = settingsCmd.ExecuteReader())
            {
                if (sReader.Read()) regexPattern = sReader.GetString(0);
            }

            if (!Regex.IsMatch(req.Password, regexPattern))
            {
                return BadRequest(new { error = "Password does not meet complexity requirements." });
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO users (username, password, access_level, needs_password_change)
                                VALUES (@user, @pass, @access, 1)";
            cmd.Parameters.AddWithValue("@user", CryptoHelper.EncryptDeterministic(req.Username));
            cmd.Parameters.AddWithValue("@pass", CryptoHelper.EncryptRandom(req.Password));
            cmd.Parameters.AddWithValue("@access", CryptoHelper.EncryptDeterministic(req.AccessLevel));
            cmd.ExecuteNonQuery();
            Database.LogAudit("USER_CREATE", User?.Identity?.Name ?? "Unknown", $"Created user {req.Username}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true });
        }

        [Authorize(Roles = "admin")]
        [Authorize(Roles = "admin,read_write")]
        [HttpPut("users/{id}/reset")]
        public IActionResult ResetPassword(long id, [FromBody] UserCreateRequest req)
        {
            using var conn = Database.GetConnection();

            string targetUsername = "Unknown";
            using (var nameCmd = conn.CreateCommand())
            {
                nameCmd.CommandText = "SELECT username FROM users WHERE id = @id";
                nameCmd.Parameters.AddWithValue("@id", id);
                var encUsername = (string)nameCmd.ExecuteScalar();
                if (encUsername != null) targetUsername = CryptoHelper.DecryptDeterministic(encUsername);
            }

            using var settingsCmd = conn.CreateCommand();
            settingsCmd.CommandText = "SELECT password_complexity_regex FROM settings WHERE id = 1";
            string regexPattern = @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$!%*#?&]{8,}$";
            using (var sReader = settingsCmd.ExecuteReader())
            {
                if (sReader.Read()) regexPattern = sReader.GetString(0);
            }

            if (!Regex.IsMatch(req.Password, regexPattern))
            {
                return BadRequest(new { error = "Password does not meet complexity requirements." });
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE users SET password = @pass, needs_password_change = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@pass", CryptoHelper.EncryptRandom(req.Password));
            cmd.Parameters.AddWithValue("@id", id);
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0) return NotFound();
            Database.LogAudit("USER_RESET", User?.Identity?.Name ?? "Unknown", $"Reset password for user {targetUsername}", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM settings WHERE id = 1";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new SettingsModel
                {
                    SessionTimeoutMinutes = reader.GetInt64(reader.GetOrdinal("session_timeout_minutes")),
                    PasswordComplexityRegex = reader.GetString(reader.GetOrdinal("password_complexity_regex")),
                    PasswordHistoryCount = reader.GetInt64(reader.GetOrdinal("password_history_count")),
                    AdEnabled = reader.GetInt64(reader.GetOrdinal("ad_enabled")),
                    AdServer = reader.GetString(reader.GetOrdinal("ad_server")),
                    AdDomain = reader.GetString(reader.GetOrdinal("ad_domain")),
                    AdServiceUser = reader.GetString(reader.GetOrdinal("ad_service_user")),
                    AdServicePassword = reader.GetString(reader.GetOrdinal("ad_service_password")),
                    AdGroupAdmin = reader.GetString(reader.GetOrdinal("ad_group_admin")),
                    AdGroupRw = reader.GetString(reader.GetOrdinal("ad_group_rw")),
                    AdGroupRo = reader.GetString(reader.GetOrdinal("ad_group_ro")),
                    EnableAuditLog = reader.GetInt64(reader.GetOrdinal("enable_audit_log")),
                    EnableAlarmLog = reader.GetInt64(reader.GetOrdinal("enable_alarm_log"))
                });
            }
            return NotFound();
        }

        [Authorize(Roles = "admin")]
        [HttpPut("settings")]
        public IActionResult UpdateSettings([FromBody] SettingsModel req)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE settings SET
                session_timeout_minutes = @p1,
                password_complexity_regex = @p2,
                password_history_count = @p3,
                ad_enabled = @p4,
                ad_server = @p5,
                ad_domain = @p6,
                ad_service_user = @p7,
                ad_service_password = @p8,
                ad_group_admin = @p9,
                ad_group_rw = @p10,
                ad_group_ro = @p11, enable_audit_log = @p12, enable_alarm_log = @p13
                WHERE id = 1";
            cmd.Parameters.AddWithValue("@p1", req.SessionTimeoutMinutes);
            cmd.Parameters.AddWithValue("@p2", req.PasswordComplexityRegex ?? "");
            cmd.Parameters.AddWithValue("@p3", req.PasswordHistoryCount);
            cmd.Parameters.AddWithValue("@p4", req.AdEnabled);
            cmd.Parameters.AddWithValue("@p5", req.AdServer ?? "");
            cmd.Parameters.AddWithValue("@p6", req.AdDomain ?? "");
            cmd.Parameters.AddWithValue("@p7", req.AdServiceUser ?? "");
            cmd.Parameters.AddWithValue("@p8", req.AdServicePassword ?? "");
            cmd.Parameters.AddWithValue("@p9", req.AdGroupAdmin ?? "");
            cmd.Parameters.AddWithValue("@p10", req.AdGroupRw ?? "");
            cmd.Parameters.AddWithValue("@p11", req.AdGroupRo ?? "");
            cmd.Parameters.AddWithValue("@p12", req.EnableAuditLog);
            cmd.Parameters.AddWithValue("@p13", req.EnableAlarmLog);
            cmd.ExecuteNonQuery();
            Database.LogAudit("SETTINGS_UPDATE", User?.Identity?.Name ?? "Unknown", $"Updated system settings", HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { success = true });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("ad-groups")]
        public IActionResult GetAdGroups()
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ad_server, ad_domain, ad_service_user, ad_service_password FROM settings WHERE id = 1";

            string adServer = "", adDomain = "", adUser = "", adPass = "";
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    adServer = reader.GetString(0);
                    adDomain = reader.GetString(1);
                    adUser = reader.GetString(2);
                    adPass = reader.GetString(3);
                }
            }

            if (string.IsNullOrWhiteSpace(adServer) || string.IsNullOrWhiteSpace(adDomain))
            {
                return BadRequest(new { error = "AD Server and Domain must be configured first." });
            }

            var groups = new List<string>();
            try
            {
                using var ldap = CreateLdapConnection(adServer);
                if (!string.IsNullOrWhiteSpace(adUser) && !string.IsNullOrWhiteSpace(adPass))
                {
                    string fqdn = adUser.Contains("@") ? adUser : $"{adUser}@{adDomain}";
                    ldap.Credential = new NetworkCredential(fqdn, adPass);
                    ldap.AuthType = AuthType.Basic;
                }
                ldap.Bind();

                string searchFilter = "(objectClass=group)";
                string baseDn = adDomain.Replace(".", ",DC=").Insert(0, "DC=");
                var searchRequest = new SearchRequest(
                    baseDn,
                    searchFilter,
                    System.DirectoryServices.Protocols.SearchScope.Subtree,
                    "distinguishedName"
                );

                // For large domains, a page result request control would be needed, but we simplify here.
                var searchResponse = (SearchResponse)ldap.SendRequest(searchRequest);
                foreach (SearchResultEntry entry in searchResponse.Entries)
                {
                    groups.Add(entry.DistinguishedName);
                }
                return Ok(groups);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Failed to connect or query AD: {ex.Message}" });
            }
        }


    }
}
