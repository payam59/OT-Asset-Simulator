using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OLRTLabSim.Data
{
    public static partial class Database
    {
        public static void LogAudit(string action, string username, string details, string ipAddress = null)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);

                // Check if audit logs are enabled in main DB
                using var conn = GetConnection();
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT enable_audit_log FROM settings WHERE id = 1";
                var result = checkCmd.ExecuteScalar();
                if (result != null && result != DBNull.Value && Convert.ToInt32(result) == 0)
                {
                    return; // Audit logs disabled
                }

                using var auditConn = GetAuditConnection();
                using var cmd = auditConn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO audit_logs (action, username, details, ip_address, timestamp)
                    VALUES (@action, @user, @details, @ip, @ts)";
                cmd.Parameters.AddWithValue("@action", action);
                cmd.Parameters.AddWithValue("@user", username);
                cmd.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ip", ipAddress ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audit Log Error] {ex.Message}");
            }
        }

        public static void LogAlarmEventToFile(string assetName, string status, string reason, string details)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);

                // Check if alarm logs are enabled
                using var conn = GetConnection();
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT enable_alarm_log FROM settings WHERE id = 1";
                var result = checkCmd.ExecuteScalar();
                if (result != null && result != DBNull.Value && Convert.ToInt32(result) == 0)
                {
                    return; // Alarm logs disabled
                }

                using var eventsConn = GetEventsConnection();
                using var cmd = eventsConn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO events_logs (asset_name, status, reason, details, timestamp)
                    VALUES (@asset, @status, @reason, @details, @ts)";
                cmd.Parameters.AddWithValue("@asset", assetName);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@reason", reason ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Alarm Log Error] {ex.Message}");
            }
        }
    }
}
