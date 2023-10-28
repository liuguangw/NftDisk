using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Liuguang.Storage;
public sealed class StorageDatabase
{
    private readonly string dbFilePath;
    private SqliteConnection? _connection;
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
        _connection = await OpenDbFileAsync(dbFilePath);
        await InitDatabaseAsync(firstInit);
    }

    public async Task CloseAsync()
    {
        if (_connection is null)
        {
            return;
        }
        await _connection.CloseAsync();
        _connection.Dispose();
        _connection = null;
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
        if (_connection is null)
        {
            return;
        }
        var command = _connection.CreateCommand();
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
        command = _connection.CreateCommand();
        command.CommandText = "CREATE INDEX file_index ON files ('parent_id' ASC, 'item_type' ASC, 'name' ASC)";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<StorageFile>> GetFileListAsync(long parentID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = _connection.CreateCommand();
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

    /// <summary>
    /// 只选择文件夹
    /// </summary>
    /// <param name="parentID"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<List<StorageFile>> GetDirListAsync(long parentID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = _connection.CreateCommand();
        var orderText = "ORDER BY name ASC";
        command.CommandText = $"SELECT * FROM files WHERE parent_id={parentID} AND item_type=0 {orderText}";
        var fileList = new List<StorageFile>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            var itemID = reader.GetInt64("id");
            var itemName = reader.GetString("name");
            var uploadTime = reader.GetInt64("upload_time");
            var file = new StorageFile(itemName)
            {
                ID = itemID,
                ParentID = parentID,
                ItemType = FileType.Dir,
                UploadTime = uploadTime
            };
            fileList.Add(file);
        }
        return fileList;
    }

    public async Task<string> GetFullPathAsync(long pathID)
    {
        if (pathID == 0)
        {
            return "/";
        }
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        StringBuilder fullPath = new();
        while (pathID != 0)
        {
            var command = _connection.CreateCommand();
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

    /// <summary>
    /// 根据ID获取文件夹或者目录的信息
    /// </summary>
    /// <param name="pathID"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<StorageFile?> GetFileInfoAsync(long pathID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        if (pathID == 0)
        {
            return null;
        }
        return await GetFileInfoAsync(pathID, _connection);
    }

    private static async Task<StorageFile?> GetFileInfoAsync(long pathID, SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM files WHERE id={pathID}";
        using var reader = await command.ExecuteReaderAsync();
        if (reader.Read())
        {
            return ReadStorageFile(reader);
        }
        return null;
    }

    private static StorageFile ReadStorageFile(SqliteDataReader reader)
    {
        var itemID = reader.GetInt64("id");
        var parentID = reader.GetInt64("parent_id");
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
        }
        //文件
        else if (fType == 1)
        {
            var cid = reader.GetString("cid");
            var size = reader.GetInt64("size");
            file.CID = cid;
            file.Size = size;
        }
        return file;
    }

    /// <summary>
    /// 获取某个目录下的指定文件或者目录的信息
    /// </summary>
    /// <param name="pathID"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<StorageFile?> GetFileInfoAsync(long pathID, string name)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        return await GetFileInfoAsync(pathID, name, _connection);
    }

    private static async Task<StorageFile?> GetFileInfoAsync(long pathID, string name, SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM files WHERE parent_id=$parent_id AND name=$name";
        command.Parameters.AddWithValue("$parent_id", pathID);
        command.Parameters.AddWithValue("$name", name);
        using var reader = await command.ExecuteReaderAsync();
        if (reader.Read())
        {
            return ReadStorageFile(reader);
        }
        return null;
    }

    public async Task InsertFileLogAsync(StorageFile fileLog)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        await InsertFileLogAsync(fileLog, _connection);
    }

    private static async Task InsertFileLogAsync(StorageFile fileLog, SqliteConnection connection)
    {
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
        //last insert id
        command = connection.CreateCommand();
        command.CommandText = "SELECT last_insert_rowid() AS last_id";
        using var reader = await command.ExecuteReaderAsync();
        if (reader.Read())
        {
            var itemID = reader.GetInt64("last_id");
            fileLog.ID = itemID;
        }
    }

    public async Task UpdateFileLogAsync(long pathID, StorageFile fileLog)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        await UpdateFileLogAsync(pathID, fileLog, _connection);
    }

    private static async Task UpdateFileLogAsync(long pathID, StorageFile fileLog, SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        var paramNames = new string[]{
            "parent_id", "item_type", "name", "cid", "size", "upload_time"
        };
        var updateStrBuilder = new StringBuilder();
        for (var i = 0; i < paramNames.Length; i++)
        {
            if (i == 0)
            {
                updateStrBuilder.AppendFormat("{0}=${0}", paramNames[i]);
            }
            else
            {
                updateStrBuilder.AppendFormat(", {0}=${0}", paramNames[i]);
            }
        }
        var updateStr = updateStrBuilder.ToString();
        command.CommandText = $"UPDATE files SET {updateStr} WHERE id=$id";
        command.Parameters.AddWithValue("$parent_id", fileLog.ParentID);
        command.Parameters.AddWithValue("$item_type", (int)fileLog.ItemType);
        command.Parameters.AddWithValue("$name", fileLog.Name);
        command.Parameters.AddWithValue("$cid", fileLog.CID);
        command.Parameters.AddWithValue("$size", fileLog.Size);
        command.Parameters.AddWithValue("$upload_time", fileLog.UploadTime);
        command.Parameters.AddWithValue("$id", pathID);
        await command.ExecuteNonQueryAsync();

    }

    /// <summary>
    /// 更新名称
    /// </summary>
    /// <param name="fId"></param>
    /// <param name="newName"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task UpdateFilenameAsync(long fId, string newName)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE files SET name = $name WHERE id = $id";
        command.Parameters.AddWithValue("$name", newName);
        command.Parameters.AddWithValue("$id", fId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteItemAsync(long fId)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM files WHERE id = $id";
        command.Parameters.AddWithValue("$id", fId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 删除文件夹和他的子项
    /// </summary>
    /// <param name="dirId"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task DeleteFolderAsync(long dirId)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        using var transaction = _connection.BeginTransaction(deferred: true);
        try
        {
            await DeleteFolderAsync(dirId, _connection);
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task DeleteFolderAsync(long dirId, SqliteConnection connection)
    {
        List<long> dirIdList = new() { dirId };
        List<long> toDeleteIdList = new();
        while (dirIdList.Count > 0)
        {
            var parentIdCondition = FormatInCondition(dirIdList);
            toDeleteIdList.Clear();
            toDeleteIdList.AddRange(dirIdList);
            dirIdList.Clear();
            var command = connection.CreateCommand();
            //获取子项列表
            command.CommandText = $"SELECT id,item_type FROM files WHERE parent_id {parentIdCondition}";
            //Trace.WriteLine(command.CommandText);
            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var subItemType = reader.GetInt32("item_type");
                var subItemId = reader.GetInt64("id");
                if (subItemType == 0)
                {
                    //目录
                    dirIdList.Add(subItemId);
                }
                else if (subItemType == 1)
                {
                    //文件
                    toDeleteIdList.Add(subItemId);
                }
            }
            var idCondition = FormatInCondition(toDeleteIdList);
            var delCommand = connection.CreateCommand();
            delCommand.CommandText = $"DELETE FROM files WHERE id {idCondition}";
            //Trace.WriteLine(delCommand.CommandText);
            await delCommand.ExecuteNonQueryAsync();
        }
    }

    private static string FormatInCondition(IEnumerable<long> itemIds)
    {
        if (itemIds.Count() < 1)
        {
            throw new Exception("Invalid params count");
        }
        else if (itemIds.Count() == 1)
        {
            var firstItemID = itemIds.LastOrDefault();
            return $"= {firstItemID}";
        }
        else
        {
            var idStrList = from tmpId in itemIds select tmpId.ToString();
            var idStr = string.Join(", ", idStrList);
            return $"IN ({idStr})";
        }
    }

    public async Task CopyFileAsync(long pathID, long destPathID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var fileInfo = await GetFileInfoAsync(pathID, _connection);
        if (fileInfo is null)
        {
            throw new Exception($"ID {pathID} not exists");
        }
        if (fileInfo.ItemType != FileType.File)
        {
            throw new Exception($"invalid file ID {pathID}");

        }
        fileInfo.ID = 0;
        fileInfo.ParentID = destPathID;
        var existFile = await GetFileInfoAsync(destPathID, fileInfo.Name, _connection);
        if (existFile is null)
        {
            //insert
            await InsertFileLogAsync(fileInfo, _connection);
        }
        else
        {
            //update
            await UpdateFileLogAsync(existFile.ID, fileInfo, _connection);
        }
    }

    public async Task CopyFolderAsync(long dirId, long destPathID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        using var transaction = _connection.BeginTransaction(deferred: true);
        try
        {
            await CopyFolderAsync(dirId, destPathID, _connection);
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CopyFolderAsync(long dirId, long destPathID, SqliteConnection connection)
    {
        //获取目录信息
        var sourceFolder = await GetFileInfoAsync(dirId, connection);
        if (sourceFolder is null)
        {
            throw new Exception($"folder id {dirId} not exists");
        }
        await CopyFolderAsync(sourceFolder, destPathID, connection);
    }
    private static async Task CopyFolderAsync(StorageFile sourceFolder, long destPathID, SqliteConnection connection)
    {
        if (sourceFolder.ItemType != FileType.Dir)
        {
            throw new Exception($"invalid folder id {sourceFolder.ID}");
        }
        //copy sourceFolder
        var sourceFolderClone = new StorageFile(sourceFolder.Name)
        {
            ID = 0,
            ParentID = destPathID,
            ItemType = FileType.Dir,
            UploadTime = sourceFolder.UploadTime
        };
        var existDir = await GetFileInfoAsync(destPathID, sourceFolder.Name, connection);
        if (existDir is null)
        {
            await InsertFileLogAsync(sourceFolderClone, connection);
        }
        else
        {
            await UpdateFileLogAsync(existDir.ID, sourceFolderClone, connection);
            sourceFolderClone.ID = existDir.ID;
        }
        //获取子项
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM files WHERE parent_id={sourceFolder.ID}";
        var subFileList = new List<StorageFile>();
        var subDirList = new List<StorageFile>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            var item = ReadStorageFile(reader);
            if (item.ItemType == FileType.File)
            {
                subFileList.Add(item);
            }
            else if (item.ItemType == FileType.Dir)
            {
                subDirList.Add(item);
            }
        }
        var subItemParentID = sourceFolderClone.ID;
        //copy 子项文件
        foreach (var subFile in subFileList)
        {
            subFile.ID = 0;
            subFile.ParentID = subItemParentID;
            var existSubFile = await GetFileInfoAsync(subItemParentID, subFile.Name, connection);
            if (existSubFile is null)
            {
                await InsertFileLogAsync(subFile, connection);
            }
            else
            {
                await UpdateFileLogAsync(existSubFile.ID, subFile, connection);
            }
        }
        //copy 子项目录
        foreach (var subDir in subDirList)
        {
            await CopyFolderAsync(subDir, subItemParentID, connection);
        }
    }

    public async Task MoveFileAsync(long pathID, long destPathID)
    {
        if (_connection is null)
        {
            throw new Exception("database error, connection is null");
        }
        var fileInfo = await GetFileInfoAsync(pathID, _connection);
        if (fileInfo is null)
        {
            throw new Exception($"ID {pathID} not exists");
        }
        var existFile = await GetFileInfoAsync(destPathID, fileInfo.Name, _connection);
        if (existFile is not null)
        {
            throw new Exception($"目标目录下已存在{existFile.Name}");
        }
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE files SET parent_id=$parent_id WHERE id = $id";
        command.Parameters.AddWithValue("$parent_id", destPathID);
        command.Parameters.AddWithValue("$id", pathID);
        await command.ExecuteNonQueryAsync();
    }
}
