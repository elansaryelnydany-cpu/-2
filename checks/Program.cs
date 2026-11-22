using Ansary;
using System.IO.Compression;
if(args.Contains("--compaction-only")){DocReaderRegression.Verify();Console.WriteLine("CHP compaction regression passed");return;}
void Check(bool ok){if(!ok)throw new Exception("Check failed");}
Check(Core.Match("العلم النافع والعمل",Core.Terms("العمل العلم")));Check(!Core.Match("العلم",Core.Terms("العلم العمل")));Check(Core.Terms("\"العلم النافع\" العمل").Length==2);
var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(Path.Combine(root,"فرعي"));try{var path=Path.Combine(root,"فرعي","عينة.DOCX");using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open());writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>العلم </w:t></w:r><w:r><w:t>النافع</w:t></w:r></w:p></w:body></w:document>");}Check(Core.Read(path)[0]=="العلم النافع");Check(Core.Scan(root,CancellationToken.None).Files.Count==1);TextFormats.Verify();Console.WriteLine("Patched DOC, DOCX, TXT, Markdown and unordered-search checks passed");}finally{Directory.Delete(root,true);}
