// See https://aka.ms/new-console-template for more information
using Liuguang.Storage;
var filePath = "G:\\soft\\demo2.jpg";
using var stream = File.OpenRead(filePath);
using var outStream = File.Create($"{filePath}.c1.car");
var container = new CarContainer1(stream);
var cidData = await container.WriteCarAsync(outStream);
Console.WriteLine("CID = {0}", CidTool.ToV0String(cidData));