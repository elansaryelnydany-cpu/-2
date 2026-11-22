using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
namespace Ansary;
public static class Core {
 public static string[] Read(string path){
  if(!Path.GetExtension(path).Equals(".docx",StringComparison.OrdinalIgnoreCase))return TextFormats.Read(path);
  using var zip=ZipFile.OpenRead(path);var entry=zip.GetEntry("word/document.xml")??throw new InvalidDataException("ليس مستند DOCX صالحًا");if(entry.Length>32*1024*1024)throw new InvalidDataException("المستند يتجاوز حد المعاينة: 32 MB XML");using var stream=entry.Open();using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32*1024*1024});var doc=XDocument.Load(reader);return doc.Descendants().Where(x=>x.Name.LocalName=="p").Select(p=>string.Concat(p.Descendants().Select(x=>x.Name.LocalName switch{"t"=>x.Value,"tab"=>"\t","br"=>"\n",_=>""}))).ToArray();
 }
 public static string[] Terms(string query)=>System.Text.RegularExpressions.Regex.Matches(query,"\"([^\"]+)\"|(\\S+)").Select(m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 public static bool Match(string text,string[] terms)=>terms.Length>0&&terms.All(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase));
 public static (List<string> Files,List<string> Errors) Scan(string root,CancellationToken token){var found=new List<string>();var errors=new List<string>();var pending=new Stack<string>();pending.Push(root);while(pending.Count>0){token.ThrowIfCancellationRequested();var dir=pending.Pop();try{foreach(var path in Directory.EnumerateFileSystemEntries(dir)){token.ThrowIfCancellationRequested();try{var attrs=File.GetAttributes(path);if((attrs&FileAttributes.ReparsePoint)!=0)continue;if((attrs&FileAttributes.Directory)!=0)pending.Push(path);else if(TextFormats.Supported(path)&&!Path.GetFileName(path).StartsWith("~$"))found.Add(path);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){errors.Add(path+": "+e.Message);}}}catch(Exception e) when(e is IOException or UnauthorizedAccessException){errors.Add(dir+": "+e.Message);}}return(found,errors);}
}
