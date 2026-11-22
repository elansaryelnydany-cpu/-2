using NPOI.HWPF.Model;
using NPOI.HWPF.SPRM;
namespace Ansary;
public static class DocReaderRegression {
 public static void Verify(){var empty=new CHPBinTable();empty.Rebuild(null);if(empty.GetTextRuns().Count!=0)throw new InvalidOperationException("Empty CHP table changed");Check(new[]{(0,5),(5,10)},10);Check(new[]{(0,3),(3,6),(6,9)},9);Check(new[]{(0,4)},4);}
 public static void ExpectOriginalFailure(){try{Verify();}catch(InvalidOperationException e) when(e.Message.Contains("Collection was modified",StringComparison.OrdinalIgnoreCase)&&e.StackTrace?.Contains("NPOI.HWPF.Model.CHPBinTable.Rebuild",StringComparison.Ordinal)==true){Console.WriteLine("EXPECTED_ORIGINAL_CHP_FAILURE: "+e.Message);return;}throw new InvalidOperationException("Unpatched reader did not reproduce the expected CHP collection-modified exception");}
 static void Check((int Start,int End)[] ranges,int end){var table=new CHPBinTable();foreach(var range in ranges){int at=table.GetTextRuns().Count;table.Insert(at,range.Start,new SprmBuffer(0));table.AdjustForInsert(at,range.End-range.Start);}var before=table.GetTextRuns();if(before.Count!=ranges.Length)throw new InvalidOperationException("Invalid regression fixture count");for(int i=0;i<ranges.Length;i++)if(before[i].Start!=ranges[i].Start||before[i].End!=ranges[i].End)throw new InvalidOperationException("Invalid regression fixture boundaries");table.Rebuild(null);var after=table.GetTextRuns();if(after.Count!=1||after[0].Start!=0||after[0].End!=end)throw new InvalidOperationException("CHP compaction lost or reordered character ranges");}
}
