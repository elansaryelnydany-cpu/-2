using System.IO;
namespace Ansary;
public sealed record DocumentSnapshot(string Path,long Length,long Ticks,string[] Paragraphs);
public sealed class DocumentCache {
 readonly object gate=new();readonly Dictionary<string,(DocumentSnapshot Doc,long Used,long Cost)> items=new(StringComparer.OrdinalIgnoreCase);const long Limit=64L*1024*1024;long used,clock;
 public DocumentSnapshot Get(string path){path=Path.GetFullPath(path);var info=new FileInfo(path);long size=info.Length,ticks=info.LastWriteTimeUtc.Ticks;lock(gate){if(items.TryGetValue(path,out var entry)&&entry.Doc.Length==size&&entry.Doc.Ticks==ticks){items[path]=(entry.Doc,++clock,entry.Cost);return entry.Doc;}}
  var text=Core.Read(path);var after=new FileInfo(path);if(after.Length!=size||after.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("تغيّر الملف أثناء قراءته؛ أعد المحاولة");var doc=new DocumentSnapshot(path,size,ticks,text);long cost=text.Sum(s=>(long)s.Length*2+32)+text.LongLength*8;
  lock(gate){var latest=new FileInfo(path);if(latest.Length!=size||latest.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("تغيّر الملف أثناء القراءة");if(items.TryGetValue(path,out var existing)&&existing.Doc.Length==size&&existing.Doc.Ticks==ticks){items[path]=(existing.Doc,++clock,existing.Cost);return existing.Doc;}if(items.Remove(path,out var old))used-=old.Cost;if(cost<=Limit){while(used+cost>Limit&&items.Count>0){var oldest=items.MinBy(e=>e.Value.Used);used-=oldest.Value.Cost;items.Remove(oldest.Key);}items[path]=(doc,++clock,cost);used+=cost;}}return doc;
 }
}
