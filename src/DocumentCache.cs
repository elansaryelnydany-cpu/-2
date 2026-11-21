using System.IO;
namespace Ansary;
public sealed record DocumentSnapshot(string Path,long Length,long Ticks,string[] Paragraphs);
public sealed class DocumentCache {
 readonly object gate=new();
 readonly Dictionary<string,(DocumentSnapshot Doc,long Used,long Cost)> items=new(StringComparer.OrdinalIgnoreCase);
 const long Limit=64L*1024*1024;long used,clock;
 public DocumentSnapshot Get(string path){lock(gate){
  path=System.IO.Path.GetFullPath(path);var info=new FileInfo(path);long size=info.Length,ticks=info.LastWriteTimeUtc.Ticks;
  if(items.TryGetValue(path,out var entry)){if(entry.Doc.Length==size&&entry.Doc.Ticks==ticks){items[path]=(entry.Doc,++clock,entry.Cost);return entry.Doc;}used-=entry.Cost;items.Remove(path);}
  var text=Core.Read(path);var after=new FileInfo(path);if(after.Length!=size||after.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("تغيّر الملف أثناء قراءته؛ أعد المحاولة");
  var doc=new DocumentSnapshot(path,size,ticks,text);long cost=text.Sum(s=>(long)s.Length*2+32)+text.LongLength*8;
  if(cost<=Limit){while(used+cost>Limit&&items.Count>0){var oldest=items.MinBy(e=>e.Value.Used);used-=oldest.Value.Cost;items.Remove(oldest.Key);}items[path]=(doc,++clock,cost);used+=cost;}return doc;
 }}
}
