// See https://aka.ms/new-console-template for more information
using Liuguang.Storage;
var filePath = "E:\\tmp\\ipfs\\Bitwarden-Portable-2022.6.0.exe";
using var stream = File.OpenRead(filePath);
using var outStream = File.Create($"{filePath}-c.car");
var container = new CarContainer(stream);
var cidData = await container.WriteCarAsync(outStream);
Console.WriteLine("CID = {0}", CidTool.ToV0String(cidData));
//
stream.Seek(0, SeekOrigin.Begin);
container.TaskNode0MaxCount = 384;
var taskTotalCount = container.TaskCount();
for (var i = 0; i < taskTotalCount; i++)
{
    using (var outStream1 = File.Create($"{filePath}-c{i}.car"))
    {
        var cidData1 = await container.WriteCarAsync(outStream1, i);
        Console.WriteLine("{0}/{1} CID = {2}", i + 1, taskTotalCount, CidTool.ToV0String(cidData1));
    }
}