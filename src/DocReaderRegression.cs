using NPOI.HWPF.Model;
using NPOI.HWPF.SPRM;
namespace Ansary;
public static class DocReaderRegression {
 public static void Verify(){var empty=new CHPBinTable();empty.Rebuild(null);if(empty.GetTextRuns().Count!=0)throw new InvalidOperationException("Empty CHP table changed");Check(new[]{(0,5),(5,10)},10);Check(new[]{(0,3),(3,6),(6,9)},9);Check(new[]{(0,4)},4);}
 static void Check((int Start,int End)[] ranges,int end){var table=new CHPBinTable();foreach(var range in ranges)table.GetTextRuns().Add(new CHPX(range.Start,range.End,new SprmBuffer(0)));table.Rebuild(null);var runs=table.GetTextRuns();if(runs.Count!=1||runs[0].Start!=0||runs[0].End!=end)throw new InvalidOperationException("CHP compaction lost or reordered character ranges");}
}
