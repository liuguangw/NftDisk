using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Liuguang.Storage;
public sealed class StorageDatabase
{
    private readonly string dbFilePath;
    private SqliteConnection? connection;
    public StorageDatabase(string filePath)
    {
        dbFilePath = filePath;
    }

    public async Task OpenAsync()
    {
        // 是否为首次初始化
        var firstInit = false;
        if (!File.Exists(dbFilePath))
        {
            firstInit = true;
            CreateDbFile(dbFilePath);
        }
        connection = await OpenDbFileAsync(dbFilePath);
        await InitDatabaseAsync(firstInit);
    }

    public async Task CloseAsync()
    {
        if (connection is null)
        {
            return;
        }
        await connection.CloseAsync();
        connection.Dispose();
        connection = null;
    }

    private static async Task<SqliteConnection> OpenDbFileAsync(string filePath)
    {
        var conn = new SqliteConnection($"Data Source={filePath}");
        await conn.OpenAsync();
        return conn;
    }

    private static void CreateDbFile(string filePath)
    {
        var fileDirPath = Path.GetDirectoryName(filePath);
        if (fileDirPath is null)
        {
            return;
        }
        if (!Directory.Exists(fileDirPath))
        {
            Directory.CreateDirectory(fileDirPath);
        }
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    /// <param name="firstInit">是否为首次初始化</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task InitDatabaseAsync(bool firstInit)
    {
        if (!firstInit)
        {
            return;
        }
        if (connection is null)
        {
            return;
        }
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        CREATE TABLE files (
        id integer NOT NULL,
        parent_id integer NOT NULL DEFAULT 0,
        item_type integer NOT NULL DEFAULT 1,
        name text NOT NULL,
        cid text NOT NULL DEFAULT '',
        size integer NOT NULL DEFAULT 0,
        upload_time integer NOT NULL DEFAULT 0,
        PRIMARY KEY (id),
        CONSTRAINT 'file_name_uni' UNIQUE (parent_id, name)
        )
        ";
        await command.ExecuteNonQueryAsync();
        //
        command = connection.CreateCommand();
        command.CommandText = "CREATE INDEX file_index ON files ('parent_id' ASC, 'item_type' ASC, 'name' ASC)";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<StorageFile>> GetFileListAsync(long parentID)
    {
        if (connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = connection.CreateCommand();
        var orderText = "ORDER BY item_type ASC, name ASC";
        command.CommandText = $"SELECT * FROM files WHERE parent_id={parentID} {orderText}";
        var fileList = new List<StorageFile>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            var itemID = reader.GetInt64("id");
            var itemName = reader.GetString("name");
            var uploadTime = reader.GetInt64("upload_time");
            var fType = reader.GetInt32("item_type");
            var file = new StorageFile(itemName)
            {
                ID = itemID,
                ParentID = parentID,
                UploadTime = uploadTime
            };
            //目录
            if (fType == 0)
            {
                file.ItemType = FileType.Dir;
                fileList.Add(file);
            }
            //文件
            else if (fType == 1)
            {
                var cid = reader.GetString("cid");
                var size = reader.GetInt64("size");
                file.CID = cid;
                file.Size = size;
                fileList.Add(file);
            }
        }
        return fileList;
    }

    public async Task<string> GetFullPathAsync(long pathID)
    {
        if (pathID == 0)
        {
            return "/";
        }
        if (connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        StringBuilder fullPath = new();
        while (pathID != 0)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT id,name,parent_id FROM files WHERE id={pathID}";
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    var tPath = string.Format("/{0}", reader.GetString("name"));
                    fullPath.Insert(0, tPath);
                }
                pathID = reader.GetInt64("parent_id");
            }
        }
        return fullPath.ToString();
    }

    public async Task<StorageFile?> GetFileInfoAsync(long pathID)
    {
        if (connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        if (pathID == 0)
        {
            return null;
        }
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT *FROM files WHERE id={pathID}";
        using var reader = await command.ExecuteReaderAsync();
        StorageFile? file = null;
        if (reader.Read())
        {
            var itemID = reader.GetInt64("id");
            var parentID = reader.GetInt64("parent_id");
            var itemName = reader.GetString("name");
            var uploadTime = reader.GetInt64("upload_time");
            var fType = reader.GetInt32("item_type");
            file = new StorageFile(itemName)
            {
                ID = itemID,
                ParentID = parentID,
                UploadTime = uploadTime
            };
            //目录
            if (fType == 0)
            {
                file.ItemType = FileType.Dir;
            }
            //文件
            else if (fType == 1)
            {
                var cid = reader.GetString("cid");
                var size = reader.GetInt64("size");
                file.CID = cid;
                file.Size = size;
            }
        }
        return file;
    }

    public async Task InsertFileLog(StorageFile fileLog)
    {
        if (connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = connection.CreateCommand();
        var paramNames = new string[]{
            "parent_id", "item_type", "name", "cid", "size", "upload_time"
        };
        var keyStr = string.Join(", ", paramNames);
        var valueStrBuilder = new StringBuilder();
        for (var i = 0; i < paramNames.Length; i++)
        {
            if (i == 0)
            {
                valueStrBuilder.Append("$" + paramNames[i]);
            }
            else
            {
                valueStrBuilder.Append(", $" + paramNames[i]);
            }
        }
        var valueStr = valueStrBuilder.ToString();
        command.CommandText = $"INSERT INTO files ({keyStr}) VALUES ({valueStr})";
        command.Parameters.AddWithValue("$parent_id", fileLog.ParentID);
        command.Parameters.AddWithValue("$item_type", (int)fileLog.ItemType);
        command.Parameters.AddWithValue("$name", fileLog.Name);
        command.Parameters.AddWithValue("$cid", fileLog.CID);
        command.Parameters.AddWithValue("$size", fileLog.Size);
        command.Parameters.AddWithValue("$upload_time", fileLog.UploadTime);
        await command.ExecuteNonQueryAsync();
    }
}
