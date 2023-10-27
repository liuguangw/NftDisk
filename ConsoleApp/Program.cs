// See https://aka.ms/new-console-template for more information
using Liuguang.Storage;
var outPath = "G:\\soft\\BaiduNetdisk_7.9.1.2.exe";
using var stream = File.OpenRead(outPath);
var container = new CarContainer(stream);
var taskCount = container.TaskCount();
for (var i = 0; i < taskCount; i++)
{
    byte[] cid;
    using (var outStream = File.Create($"{outPath}.{i}.car"))
    {
        cid = await container.RunCarTaskAsync(outStream, i, default);
    }
    Console.WriteLine("CID = {0}", CidTool.ToV0String(cid));
}