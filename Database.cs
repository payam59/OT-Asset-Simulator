using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using OLRTLabSim.Helpers;

namespace OLRTLabSim.Data
{
    public static partial class Database
    {
        public static readonly string DbFile = "lab_assets.db";
        public static readonly string AuditDbFile = "auditlog.db";
        public static readonly string EventsDbFile = "eventslog.db";
        public static readonly string LogDir = "simulation_logs";

        public static void InitDb()
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);

            // Initialize main database
            using (var conn = GetConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS bbmd (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT UNIQUE NOT NULL,
                            description TEXT,
                            port INTEGER UNIQUE NOT NULL,
                            device_id INTEGER NOT NULL,
                            ip_address TEXT DEFAULT '0.0.0.0',
                            enabled INTEGER DEFAULT 1,
                            created_at REAL DEFAULT 0.0
                        )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS assets (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT UNIQUE,
                            asset_name TEXT,
                            tag_name TEXT,
                            type TEXT,
                            sub_type TEXT DEFAULT 'Analog',
                            protocol TEXT,
                            address INTEGER,
                            min_range REAL DEFAULT 0.0,
                            max_range REAL DEFAULT 100.0,
                            current_value REAL DEFAULT 0.0,
                            drift_rate REAL DEFAULT 0.1,
                            manual_override INTEGER DEFAULT 0,
                            icon TEXT,
                            filename TEXT,
                            bacnet_port INTEGER DEFAULT 47808,
                            bacnet_device_id INTEGER DEFAULT 1234,
                            is_normally_open INTEGER DEFAULT 1,
                            change_probability REAL DEFAULT 0.0,
                            change_interval INTEGER DEFAULT 15,
                            last_flip_check REAL DEFAULT 0.0,
                            bbmd_id INTEGER,
                            object_type TEXT DEFAULT 'value',
                            bacnet_properties TEXT DEFAULT '{}',
                            modbus_unit_id INTEGER DEFAULT 1,
                            modbus_register_type TEXT DEFAULT 'holding',
                            modbus_ip TEXT DEFAULT '0.0.0.0',
                            modbus_port INTEGER DEFAULT 5020,
                            modbus_alarm_address INTEGER,
                            modbus_alarm_bit INTEGER DEFAULT 0,
                            modbus_zero_based INTEGER DEFAULT 1,
                            modbus_word_order TEXT DEFAULT 'low_high',
                            dnp3_ip TEXT DEFAULT '0.0.0.0',
                            dnp3_port INTEGER DEFAULT 20000,
                            dnp3_outstation_address INTEGER DEFAULT 10,
                            dnp3_master_address INTEGER DEFAULT 1,
                            dnp3_point_class TEXT DEFAULT 'analog_output',
                            dnp3_event_class INTEGER DEFAULT 1,
                            dnp3_static_variation INTEGER DEFAULT 0,
                            alarm_state INTEGER DEFAULT 0,
                            alarm_message TEXT,
                            FOREIGN KEY (bbmd_id) REFERENCES bbmd(id) ON DELETE SET NULL
                        )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS alarm_events (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            asset_id INTEGER NOT NULL,
                            asset_name TEXT NOT NULL,
                            message TEXT NOT NULL,
                            active INTEGER DEFAULT 1,
                            created_at REAL NOT NULL,
                            cleared_at REAL,
                            FOREIGN KEY (asset_id) REFERENCES assets(id) ON DELETE CASCADE
                        )";
                    cmd.ExecuteNonQuery();


                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS users (

                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            username TEXT UNIQUE NOT NULL,
                            password TEXT NOT NULL,
                            access_level TEXT NOT NULL,
                            needs_password_change INTEGER DEFAULT 1,
                            expiry_date REAL
                        )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS password_history (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            user_id INTEGER NOT NULL,
                            password TEXT NOT NULL,
                            changed_at REAL NOT NULL,
                            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
                        )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS settings (
                            id INTEGER PRIMARY KEY CHECK (id = 1),
                            session_timeout_minutes INTEGER DEFAULT 60,
                            password_complexity_regex TEXT DEFAULT '^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$!%*#?&]{8,}$',
                            password_history_count INTEGER DEFAULT 3,
                            ad_enabled INTEGER DEFAULT 0,
                            ad_server TEXT DEFAULT '',
                            ad_domain TEXT DEFAULT '',
                            ad_service_user TEXT DEFAULT '',
                            ad_service_password TEXT DEFAULT '',
                            ad_group_admin TEXT DEFAULT '',
                            ad_group_rw TEXT DEFAULT '',
                            ad_group_ro TEXT DEFAULT '',
                            enable_audit_log INTEGER DEFAULT 1,
                            enable_alarm_log INTEGER DEFAULT 1,
                            password_min_length INTEGER DEFAULT 8,
                            require_uppercase INTEGER DEFAULT 1,
                            require_lowercase INTEGER DEFAULT 1,
                            require_number INTEGER DEFAULT 1,
                            require_special INTEGER DEFAULT 1
                        )";
                    cmd.ExecuteNonQuery();
                }

                // Alter settings table if necessary
                using (var cmd = conn.CreateCommand())
                {
                    List<string> setCols = new List<string>();
                    cmd.CommandText = "PRAGMA table_info(settings)";
                    using (var r = cmd.ExecuteReader()) { while(r.Read()) setCols.Add(r["name"].ToString()); }
                    if (!setCols.Contains("ad_service_user")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN ad_service_user TEXT DEFAULT ''";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("ad_service_password")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN ad_service_password TEXT DEFAULT ''";
                        cmd.ExecuteNonQuery();
                    }

                    if (!setCols.Contains("enable_audit_log")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN enable_audit_log INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("enable_alarm_log")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN enable_alarm_log INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("password_min_length")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN password_min_length INTEGER DEFAULT 8";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("require_uppercase")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN require_uppercase INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("require_lowercase")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN require_lowercase INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("require_number")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN require_number INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                    if (!setCols.Contains("require_special")) {
                        cmd.CommandText = "ALTER TABLE settings ADD COLUMN require_special INTEGER DEFAULT 1";
                        cmd.ExecuteNonQuery();
                    }
                }


                using (var cmd = conn.CreateCommand())
                {
                    var userCols = new List<string>();
                    cmd.CommandText = "PRAGMA table_info(users)";
                    using (var r = cmd.ExecuteReader()) { while (r.Read()) userCols.Add(r["name"].ToString()); }
                    if (!userCols.Contains("expiry_date"))
                    {
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN expiry_date REAL";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Seed default settings if they don't exist

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM settings WHERE id = 1";
                    var count = Convert.ToInt64(cmd.ExecuteScalar());
                    if (count == 0)
                    {
                        cmd.CommandText = "INSERT INTO settings (id) VALUES (1)";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Seed default admin user if it doesn't exist
                using (var cmd = conn.CreateCommand())
                {
                    string encAdminUser = CryptoHelper.EncryptDeterministic("admin");
                    cmd.CommandText = "SELECT COUNT(*) FROM users WHERE username = @username";
                    cmd.Parameters.AddWithValue("@username", encAdminUser);
                    var count = Convert.ToInt64(cmd.ExecuteScalar());

                    if (count == 0)
                    {
                        cmd.CommandText = @"INSERT INTO users (username, password, access_level, needs_password_change)
                                            VALUES (@user, @pass, @access, 1)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@user", encAdminUser);
                        cmd.Parameters.AddWithValue("@pass", CryptoHelper.EncryptRandom("admin"));
                        cmd.Parameters.AddWithValue("@access", CryptoHelper.EncryptDeterministic("admin"));
                        cmd.ExecuteNonQuery();
                    }
                }

                // SQLite doesn't natively support easy ALTER TABLE for multiple columns in old versions
                // But for basic string/int appends we can do this simply
                List<string> columns = new List<string>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(assets)";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(reader["name"].ToString());
                        }
                    }
                }

                var migrations = new Dictionary<string, string>
                {
                    { "asset_name", "TEXT" },
                    { "tag_name", "TEXT" },
                    { "sub_type", "TEXT DEFAULT 'Analog'" },
                    { "is_normally_open", "INTEGER DEFAULT 1" },
                    { "bacnet_port", "INTEGER DEFAULT 47808" },
                    { "bacnet_device_id", "INTEGER DEFAULT 1234" },
                    { "filename", "TEXT" },
                    { "change_probability", "REAL DEFAULT 0.0" },
                    { "change_interval", "INTEGER DEFAULT 15" },
                    { "last_flip_check", "REAL DEFAULT 0.0" },
                    { "bbmd_id", "INTEGER" },
                    { "object_type", "TEXT DEFAULT 'value'" },
                    { "bacnet_properties", "TEXT DEFAULT '{}'" },
                    { "modbus_unit_id", "INTEGER DEFAULT 1" },
                    { "modbus_register_type", "TEXT DEFAULT 'holding'" },
                    { "modbus_ip", "TEXT DEFAULT '0.0.0.0'" },
                    { "modbus_port", "INTEGER DEFAULT 5020" },
                    { "modbus_alarm_address", "INTEGER" },
                    { "modbus_alarm_bit", "INTEGER DEFAULT 0" },
                    { "modbus_zero_based", "INTEGER DEFAULT 1" },
                    { "modbus_word_order", "TEXT DEFAULT 'low_high'" },
                    { "dnp3_ip", "TEXT DEFAULT '0.0.0.0'" },
                    { "dnp3_port", "INTEGER DEFAULT 20000" },
                    { "dnp3_outstation_address", "INTEGER DEFAULT 10" },
                    { "dnp3_master_address", "INTEGER DEFAULT 1" },
                    { "dnp3_point_class", "TEXT DEFAULT 'analog_output'" },
                    { "dnp3_event_class", "INTEGER DEFAULT 1" },
                    { "dnp3_static_variation", "INTEGER DEFAULT 0" },
                    { "alarm_state", "INTEGER DEFAULT 0" },
                    { "alarm_message", "TEXT" }
                };

                using (var cmd = conn.CreateCommand())
                {
                    foreach (var kvp in migrations)
                    {
                        if (!columns.Contains(kvp.Key))
                        {
                            cmd.CommandText = $"ALTER TABLE assets ADD COLUMN {kvp.Key} {kvp.Value}";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE assets SET asset_name = name WHERE asset_name IS NULL OR TRIM(asset_name) = ''";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "UPDATE assets SET tag_name = name WHERE tag_name IS NULL OR TRIM(tag_name) = ''";
                    cmd.ExecuteNonQuery();
                }
            }

            // Initialize audit log database
            using (var conn = GetAuditConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS audit_logs (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            action TEXT NOT NULL,
                            username TEXT NOT NULL,
                            details TEXT,
                            ip_address TEXT,
                            timestamp REAL NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }
            }

            // Initialize event log database
            using (var conn = GetEventsConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS events_logs (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            asset_name TEXT NOT NULL,
                            status TEXT NOT NULL,
                            reason TEXT,
                            details TEXT,
                            timestamp REAL NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection($"Data Source={DbFile};");
            conn.Open();
            return conn;
        }

        public static SqliteConnection GetAuditConnection()
        {
            var conn = new SqliteConnection($"Data Source={AuditDbFile};");
            conn.Open();
            return conn;
        }

        public static SqliteConnection GetEventsConnection()
        {
            var conn = new SqliteConnection($"Data Source={EventsDbFile};");
            conn.Open();
            return conn;
        }

        public static double GetCurrentUnixTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (DateTimeOffset.UtcNow.Millisecond / 1000.0);
        }
    }
}
