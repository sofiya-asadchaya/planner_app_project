using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

namespace Planner_app
{
    public class SQLiteAppStateStorage : IAppStateStorage
    {
        private readonly string _connectionString;

        public SQLiteAppStateStorage(string databasePath)
        {
            // Initialize SQLitePCL
            SQLitePCL.Batteries.Init();

            _connectionString = $"Data Source={databasePath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTaskBlocksTable = @"
            CREATE TABLE IF NOT EXISTS TaskBlocks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Text TEXT,
                Color TEXT,
                ParentPanel TEXT,
                ParentDate TEXT,
                LocationX INTEGER,
                LocationY INTEGER,
                Width INTEGER,
                Height INTEGER,
                Visible INTEGER
            )";

            string createCopyBlocksTable = @"
            CREATE TABLE IF NOT EXISTS CopyBlocks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Text TEXT,
                Color TEXT,
                ParentPanel TEXT,
                ParentDate TEXT NULL, 
                LocationX INTEGER,
                LocationY INTEGER,
                Width INTEGER,
                Height INTEGER,
                Visible INTEGER
            )";

            using var command = connection.CreateCommand();
            command.CommandText = createTaskBlocksTable;
            command.ExecuteNonQuery();

            command.CommandText = createCopyBlocksTable;
            command.ExecuteNonQuery();
        }

        public void SaveAppState(AppState state)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            // Очистка таблиц
            var clearTaskBlocks = "DELETE FROM TaskBlocks";
            var clearCopyBlocks = "DELETE FROM CopyBlocks";

            using var clearCommand = connection.CreateCommand();
            clearCommand.CommandText = clearTaskBlocks;
            clearCommand.ExecuteNonQuery();

            clearCommand.CommandText = clearCopyBlocks;
            clearCommand.ExecuteNonQuery();

            // Сохранение TaskBlocks
            var insertTaskBlock = @"
            INSERT INTO TaskBlocks (Text, Color, ParentPanel, ParentDate, LocationX, LocationY, Width, Height, Visible)
            VALUES (@Text, @Color, @ParentPanel, @ParentDate, @LocationX, @LocationY, @Width, @Height, @Visible)";

            foreach (var block in state.TaskBlocks)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertTaskBlock;
                insertCommand.Parameters.AddWithValue("@Text", block.Text);
                insertCommand.Parameters.AddWithValue("@Color", block.Color);
                insertCommand.Parameters.AddWithValue("@ParentPanel", block.ParentPanel);
                insertCommand.Parameters.AddWithValue("@ParentDate", block.ParentDate?.ToString("o"));
                insertCommand.Parameters.AddWithValue("@LocationX", block.LocationX);
                insertCommand.Parameters.AddWithValue("@LocationY", block.LocationY);
                insertCommand.Parameters.AddWithValue("@Width", block.Width);
                insertCommand.Parameters.AddWithValue("@Height", block.Height);
                insertCommand.Parameters.AddWithValue("@Visible", block.Visible ? 1 : 0);
                insertCommand.ExecuteNonQuery();
            }

            // Сохранение CopyBlocks
            var insertCopyBlock = @"
            INSERT INTO CopyBlocks (Text, Color, ParentPanel, ParentDate, LocationX, LocationY, Width, Height, Visible)
            VALUES (@Text, @Color, @ParentPanel, @ParentDate, @LocationX, @LocationY, @Width, @Height, @Visible)";

            foreach (var block in state.CopyBlocks)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertCopyBlock;
                insertCommand.Parameters.AddWithValue("@Text", block.Text);
                insertCommand.Parameters.AddWithValue("@Color", block.Color);
                insertCommand.Parameters.AddWithValue("@ParentPanel", block.ParentPanel);
                insertCommand.Parameters.AddWithValue("@ParentDate", DBNull.Value);
                insertCommand.Parameters.AddWithValue("@LocationX", block.LocationX);
                insertCommand.Parameters.AddWithValue("@LocationY", block.LocationY);
                insertCommand.Parameters.AddWithValue("@Width", block.Width);
                insertCommand.Parameters.AddWithValue("@Height", block.Height);
                insertCommand.Parameters.AddWithValue("@Visible", block.Visible ? 1 : 0);
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public AppState LoadAppState()
        {
            var state = new AppState();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Загрузка TaskBlocks
            var selectTaskBlocks = "SELECT * FROM TaskBlocks";
            using var taskCommand = connection.CreateCommand();
            taskCommand.CommandText = selectTaskBlocks;

            using var taskReader = taskCommand.ExecuteReader();
            while (taskReader.Read())
            {
                state.TaskBlocks.Add(new TaskBlockState
                {
                    Text = taskReader.GetString(1),
                    Color = taskReader.GetString(2),
                    ParentPanel = taskReader.GetString(3),
                    ParentDate = taskReader.IsDBNull(4) ? null : DateTime.Parse(taskReader.GetString(4)),
                    LocationX = taskReader.GetInt32(5),
                    LocationY = taskReader.GetInt32(6),
                    Width = taskReader.GetInt32(7),
                    Height = taskReader.GetInt32(8),
                    Visible = taskReader.GetInt32(9) == 1
                });
            }

            // Загрузка CopyBlocks
            var selectCopyBlocks = "SELECT * FROM CopyBlocks";
            using var copyCommand = connection.CreateCommand();
            copyCommand.CommandText = selectCopyBlocks;

            using var copyReader = copyCommand.ExecuteReader();
            while (copyReader.Read())
            {
                state.CopyBlocks.Add(new TaskBlockState
                {
                    Text = copyReader.GetString(1),
                    Color = copyReader.GetString(2),
                    ParentPanel = copyReader.GetString(3),
                    ParentDate = copyReader.IsDBNull(4) ? null : DateTime.Parse(copyReader.GetString(4)),
                    LocationX = copyReader.GetInt32(5),
                    LocationY = copyReader.GetInt32(6),
                    Width = copyReader.GetInt32(7),
                    Height = copyReader.GetInt32(8),
                    Visible = copyReader.GetInt32(9) == 1
                });
            }

            return state;
        }
    }

}
