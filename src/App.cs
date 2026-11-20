using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
namespace Ansary;
public sealed class App:Application {
 [STAThread] public static void Main()=>new App().Run(new LibraryWindow());
}
public sealed class LibraryWindow:Window {
 readonly TreeView library=new(),results=new();
 readonly RichTextBox viewer=new(){IsReadOnly=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 readonly TextBox query=new(){MinWidth=180};
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,MaxHeight=60};
 readonly List<string> files=new(),errors=new();readonly HashSet<string> selected=new(StringComparer.OrdinalIgnoreCase);
 readonly StackPanel tools=new(){Orientation=Orientation.Horizontal};
 readonly List<Button> workButtons=new();CancellationTokenSource? cancel;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ElansaryWordLibrary","files.json");
 string[] terms=Array.Empty<string>();
 record Hit(string Path,int Index,string Text);
 public LibraryWindow(){
  Title="الأنصاري — مكتبة Word المستقلة — معاينة DOCX";Width=1300;Height=850;MinWidth=850;MinHeight=550;FlowDirection=FlowDirection.RightToLeft;
  var root=new DockPanel{Margin=new Thickness(10)};Content=root;
  DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
  Add("إضافة مجلد",async()=>await Folder());Add("إضافة ملفات",()=>{Import();return Task.CompletedTask;});
  tools.Children.Add(query);Add("بحث",Search);Add("إلغاء",()=>{cancel?.Cancel();return Task.CompletedTask;},false);
  Add("طي النتائج",()=>{Expand(false);return Task.CompletedTask;},false);Add("فرد النتائج",()=>{Expand(true);return Task.CompletedTask;},false);
  Add("تقرير",()=>{MessageBox.Show(errors.Count==0?"لا توجد أخطاء مسجلة":string.Join("\n",errors.Take(100)),"تقرير");return Task.CompletedTask;},false);
  DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
  var grid=new Grid();root.Children.Add(grid);
  foreach(var width in new[]{2d,0,3,0,5})grid.ColumnDefinitions.Add(width==0?new ColumnDefinition{Width=new GridLength(5)}:new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});
  grid.Children.Add(library);Grid.SetColumn(results,2);grid.Children.Add(results);Grid.SetColumn(viewer,4);grid.Children.Add(viewer);
  foreach(var col in new[]{1,3}){var splitter=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch};Grid.SetColumn(splitter,col);grid.Children.Add(splitter);}
  results.SelectedItemChanged+=async(_,e)=>{if(e.NewValue is TreeViewItem item&&item.Tag is Hit hit)await Show(hit);};
  query.KeyDown+=async(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter&&cancel==null)await Search();};
  Closing+=(_,_)=>cancel?.Cancel();
  try{if(File.Exists(settings))files.AddRange((JsonSerializer.Deserialize<string[]>(File.ReadAllText(settings))??Array.Empty<string>()).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase));}catch(Exception e){errors.Add("تحميل المكتبة: "+e.Message);}
  Rebuild();status.Text="DOCX فقط — عارض نصي للقراءة، لا يغيّر مستنداتك. حد النتائج المعروضة 2000.";
 }
 void Add(string text,Func<Task> action,bool busy=true){var b=new Button{Content=text,Margin=new Thickness(3),Padding=new Thickness(7)};tools.Children.Add(b);if(busy)workButtons.Add(b);b.Click+=async(_,_)=>{try{await action();}catch(Exception e){errors.Add(e.Message);status.Text=e.Message;}};}
 async Task Run(Func<CancellationToken,Task> action){if(cancel!=null)return;cancel=new();foreach(var b in workButtons)b.IsEnabled=false;try{await action(cancel.Token);}catch(OperationCanceledException){status.Text="تم الإلغاء";}catch(Exception e){errors.Add(e.Message);status.Text="تعذر الإكمال: "+e.Message;}finally{cancel.Dispose();cancel=null;foreach(var b in workButtons)b.IsEnabled=true;}}
 async Task Folder(){var dialog=new OpenFolderDialog{Title="اختر مجلد ملفات DOCX"};if(dialog.ShowDialog(this)!=true)return;await Run(async token=>{status.Text="جار فحص المجلد وفروعه…";var scan=await Task.Run(()=>Core.Scan(dialog.FolderName,token),token);errors.AddRange(scan.Errors);Merge(scan.Files);});}
 void Import(){var dialog=new OpenFileDialog{Filter="Word DOCX|*.docx",Multiselect=true};if(dialog.ShowDialog(this)==true)Merge(dialog.FileNames);}
 void Merge(IEnumerable<string> paths){var known=new HashSet<string>(files,StringComparer.OrdinalIgnoreCase);foreach(var path in paths)if(known.Add(path)){files.Add(path);selected.Add(path);}Rebuild();try{Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,JsonSerializer.Serialize(files));}catch(Exception e){errors.Add("حفظ قائمة المكتبة: "+e.Message);}status.Text=$"ملفات المكتبة: {files.Count} | أخطاء مسجلة: {errors.Count}";}
 void Rebuild(){library.Items.Clear();foreach(var group in files.OrderBy(p=>p).GroupBy(p=>Path.GetDirectoryName(p)??"")){var folder=new TreeViewItem{Header=group.Key,IsExpanded=true};library.Items.Add(folder);foreach(var path in group){var check=new CheckBox{Content=Path.GetFileName(path),IsChecked=selected.Contains(path),ToolTip=path,Margin=new Thickness(3)};check.Checked+=(_,_)=>selected.Add(path);check.Unchecked+=(_,_)=>selected.Remove(path);folder.Items.Add(check);}}}
 async Task Search(){
  var requested=Core.Terms(query.Text);if(requested.Length==0){status.Text="اكتب كلمات البحث";return;}
  var scope=(selected.Count==0?files:files.Where(selected.Contains)).ToArray();if(scope.Length==0){status.Text="أضف ملفات أو مجلدًا أولًا";return;}
  await Run(async token=>{results.Items.Clear();status.Text="جار البحث — المحدد فقط، أو المكتبة كلها إن لم تحدد ملفات…";
   var scan=await Task.Run(()=>{var hits=new List<Hit>();var failures=new List<string>();bool capped=false;
    foreach(var path in scope){token.ThrowIfCancellationRequested();try{var paragraphs=Core.Read(path);for(int i=0;i<paragraphs.Length;i++){token.ThrowIfCancellationRequested();if(Core.Match(paragraphs[i],requested)){if(hits.Count==2000){capped=true;break;}hits.Add(new Hit(path,i,paragraphs[i]));}}}catch(Exception e) when(e is not OperationCanceledException){failures.Add(path+": "+e.Message);}if(capped)break;}
    return(hits,failures,capped);},token);
   terms=requested;errors.AddRange(scan.failures);
   foreach(var folder in scan.hits.GroupBy(h=>Path.GetDirectoryName(h.Path)??"")){var branch=new TreeViewItem{Header=folder.Key,IsExpanded=true};results.Items.Add(branch);foreach(var file in folder.GroupBy(h=>h.Path)){var node=new TreeViewItem{Header=$"{Path.GetFileName(file.Key)} ({file.Count()})",IsExpanded=true};branch.Items.Add(node);foreach(var hit in file){var row=new StackPanel();var copy=new Button{Content="نسخ",HorizontalAlignment=HorizontalAlignment.Left};copy.Click+=(_,_)=>{try{Clipboard.SetText(hit.Path+"\n"+hit.Text);}catch(Exception e){status.Text=e.Message;}};row.Children.Add(copy);var snippet=new TextBlock{TextWrapping=TextWrapping.Wrap,MaxWidth=420};Paint(snippet.Inlines,$"فقرة {hit.Index+1}: {hit.Text}",requested);row.Children.Add(snippet);node.Items.Add(new TreeViewItem{Header=row,Tag=hit});}}}
   status.Text=$"النتائج المعروضة: {scan.hits.Count} | أخطاء القراءة: {scan.failures.Count}"+(scan.capped?" | وصلت إلى حد 2000؛ ضيّق البحث":"");
  });
 }
 async Task Show(Hit hit){try{var paragraphs=await Task.Run(()=>Core.Read(hit.Path));var doc=new FlowDocument{FlowDirection=FlowDirection.RightToLeft,FontSize=18,PageWidth=800};Paragraph? target=null;for(int i=0;i<paragraphs.Length;i++){var p=new Paragraph();Paint(p.Inlines,paragraphs[i],terms);if(i==hit.Index){p.Background=Brushes.LightCyan;target=p;}doc.Blocks.Add(p);}viewer.Document=doc;target?.BringIntoView();}catch(Exception e){errors.Add(e.Message);status.Text="تعذر عرض الملف: "+e.Message;}}
 static void Paint(InlineCollection output,string text,string[] words){var colors=new[]{Brushes.Yellow,Brushes.LightGreen,Brushes.LightPink,Brushes.LightSkyBlue};int at=0;while(at<text.Length){int next=-1,which=-1;for(int i=0;i<words.Length;i++){int pos=text.IndexOf(words[i],at,StringComparison.OrdinalIgnoreCase);if(pos>=0&&(next<0||pos<next)){next=pos;which=i;}}if(next<0){output.Add(new Run(text[at..]));break;}if(next>at)output.Add(new Run(text[at..next]));output.Add(new Run(text.Substring(next,words[which].Length)){Background=colors[which%colors.Length],Foreground=Brushes.Black});at=next+words[which].Length;}}
 void Expand(bool value){void Visit(ItemsControl parent){foreach(var child in parent.Items)if(child is TreeViewItem node){node.IsExpanded=value;Visit(node);}}Visit(results);}
}
