using System.IO;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using W = DocumentFormat.OpenXml.Wordprocessing;
namespace Ansary.Editor;
public sealed record OutlineItem(int Level,string Text,Paragraph Paragraph);
public sealed class ElansaryEditorWindow:Window {
 readonly RichTextBox editor=new(){AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Arial"),FontSize=14,Padding=new Thickness(10)};
 readonly TextBox searchBox=new(){MinWidth=220,Margin=new Thickness(2)};
 readonly TextBox outlineSearch=new(){MinWidth=140,Margin=new Thickness(2)};
 readonly StackPanel searchChips=new(){Orientation=Orientation.Horizontal};
 readonly StackPanel outlineChips=new(){Orientation=Orientation.Horizontal};
 readonly Border searchHistoryPanel=new(){Visibility=Visibility.Collapsed,Background=Brushes.LemonChiffon,BorderBrush=Brushes.Goldenrod,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(4),Margin=new Thickness(0,2,0,2)};
 readonly Border outlineHistoryPanel=new(){Visibility=Visibility.Collapsed,Background=Brushes.LemonChiffon,BorderBrush=Brushes.Goldenrod,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(4),Margin=new Thickness(0,2,0,2)};
 readonly TreeView outlineTree=new(){MinHeight=200,Background=Brushes.White};
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};
 readonly List<TextRange> highlightRanges=new();
 readonly List<(Paragraph Para,List<TextRange> Ranges)> matches=new();
 readonly string baseDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ElansaryWordLibrary");
 int currentMatch=-1;string? currentPath;bool dirty;
 static readonly string[] CapsuleColors={"#6A4FD1","#16754B","#2563A6","#B03333","#9A5600","#8C4B16","#7A3E9D","#1F6F8B"};
 public ElansaryEditorWindow(){
  Title="محرر وعارض الأنصاري لويندوز";Width=1250;Height=850;MinWidth=900;MinHeight=600;FlowDirection=FlowDirection.RightToLeft;
  var root=new DockPanel{Margin=new Thickness(8)};Content=root;
  DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
  var main=new Grid();root.Children.Add(main);
  main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});
  main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(5)});
  main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  var outlineHost=new DockPanel{Margin=new Thickness(0,0,5,0)};Grid.SetColumn(outlineHost,0);main.Children.Add(outlineHost);
  var outlineTop=new StackPanel();DockPanel.SetDock(outlineTop,Dock.Top);outlineHost.Children.Add(outlineTop);
  outlineTop.Children.Add(new TextBlock{Text="المخطط التفصيلي",FontWeight=FontWeights.Bold,Margin=new Thickness(2)});
  var outlineRow=new StackPanel{Orientation=Orientation.Horizontal};outlineTop.Children.Add(outlineRow);
  outlineRow.Children.Add(outlineSearch);
  Capsule(LocalButton(outlineRow,"بحث",SearchOutline),"#2563A6");
  Capsule(LocalButton(outlineRow,"مزامنة",SyncOutline),"#16754B");
  Capsule(LocalButton(outlineRow,"طي/فرد",ToggleOutlineFold),"#6A4FD1");
  Capsule(LocalButton(outlineRow,"سجل",()=>TogglePanel(outlineHistoryPanel,outlineChips,"outline",outlineSearch,t=>SearchOutline())),"#9A5600");
  outlineHistoryPanel.Child=new ScrollViewer{Content=outlineChips,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=70};
  outlineTop.Children.Add(outlineHistoryPanel);
  outlineHost.Children.Add(outlineTree);
  var editorHost=new DockPanel();Grid.SetColumn(editorHost,2);main.Children.Add(editorHost);
  var fileBar=new WrapPanel();DockPanel.SetDock(fileBar,Dock.Top);editorHost.Children.Add(fileBar);
  Capsule(LocalButton(fileBar,"مستند جديد",NewDoc),"#16754B");
  Capsule(LocalButton(fileBar,"فتح",OpenDoc),"#2563A6");
  Capsule(LocalButton(fileBar,"حفظ",SaveDoc),"#6A4FD1");
  Capsule(LocalButton(fileBar,"حفظ باسم",SaveAsDoc),"#7A3E9D");
  Capsule(LocalButton(fileBar,"تصدير PDF",ExportPdf),"#B03333");
  var formatBar=new WrapPanel();DockPanel.SetDock(formatBar,Dock.Top);editorHost.Children.Add(formatBar);
  var fontBox=new ComboBox{Width=120,Margin=new Thickness(2),ItemsSource=Fonts.SystemFontFamilies.Select(f=>f.Source).OrderBy(s=>s).ToList()};formatBar.Children.Add(fontBox);fontBox.SelectionChanged+=(_,_)=>{if(fontBox.SelectedItem is string f)ApplyToSelection(t=>t.ApplyPropertyValue(TextElement.FontFamilyProperty,new FontFamily(f)));};
  var sizeBox=new ComboBox{Width=60,Margin=new Thickness(2),ItemsSource=new[]{8,9,10,11,12,14,16,18,20,24,28,32,36,48},SelectedIndex=5};formatBar.Children.Add(sizeBox);sizeBox.SelectionChanged+=(_,_)=>{if(sizeBox.SelectedItem is int s)ApplyToSelection(t=>t.ApplyPropertyValue(TextElement.FontSizeProperty,(double)s));};
  Capsule(LocalButton(formatBar,"عريض",()=>ApplyToSelection(t=>t.ApplyPropertyValue(TextElement.FontWeightProperty,t.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight w&&w==FontWeights.Bold?FontWeights.Normal:FontWeights.Bold))),"#2563A6");
  Capsule(LocalButton(formatBar,"مائل",()=>ApplyToSelection(t=>t.ApplyPropertyValue(TextElement.FontStyleProperty,t.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle s&&s==FontStyles.Italic?FontStyles.Normal:FontStyles.Italic))),"#2563A6");
  Capsule(LocalButton(formatBar,"تسطير",()=>ApplyToSelection(t=>t.ApplyPropertyValue(Inline.TextDecorationsProperty,t.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection d&&d.Count>0?null:TextDecorations.Underline))),"#2563A6");
  var colorBox=new ComboBox{Width=90,Margin=new Thickness(2),ItemsSource=new[]{"أسود","أحمر","أزرق","أخضر","برتقالي","بنفسجي"}};formatBar.Children.Add(colorBox);colorBox.SelectionChanged+=(_,_)=>{var colors=new[]{"#000000","#D32F2F","#1565C0","#2E7D32","#EF6C00","#6A1B9A"};if(colorBox.SelectedIndex>=0)ApplyToSelection(t=>t.ApplyPropertyValue(TextElement.ForegroundProperty,(Brush)new BrushConverter().ConvertFromString(colors[colorBox.SelectedIndex])!));};
  Capsule(LocalButton(formatBar,"يمين",()=>ApplyToParagraph(p=>p.TextAlignment=TextAlignment.Right)),"#5B6B23");
  Capsule(LocalButton(formatBar,"وسط",()=>ApplyToParagraph(p=>p.TextAlignment=TextAlignment.Center)),"#5B6B23");
  Capsule(LocalButton(formatBar,"يسار",()=>ApplyToParagraph(p=>p.TextAlignment=TextAlignment.Left)),"#5B6B23");
  Capsule(LocalButton(formatBar,"عنوان 1",()=>SetHeading(1)),"#6A4FD1");
  Capsule(LocalButton(formatBar,"عنوان 2",()=>SetHeading(2)),"#6A4FD1");
  Capsule(LocalButton(formatBar,"عنوان 3",()=>SetHeading(3)),"#6A4FD1");
  var searchBar=new WrapPanel();DockPanel.SetDock(searchBar,Dock.Top);editorHost.Children.Add(searchBar);
  searchBar.Children.Add(new TextBlock{Text="بحث في المستند:",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4)});
  searchBar.Children.Add(searchBox);
  Capsule(LocalButton(searchBar,"بحث",()=>Search(true)),"#2563A6");
  Capsule(LocalButton(searchBar,"السابق",()=>Navigate(-1)),"#1F6F8B");
  Capsule(LocalButton(searchBar,"التالي",()=>Navigate(1)),"#1F6F8B");
  Capsule(LocalButton(searchBar,"سجل",()=>TogglePanel(searchHistoryPanel,searchChips,"search",searchBox,t=>Search(true))),"#9A5600");
  searchHistoryPanel.Child=new ScrollViewer{Content=searchChips,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=70};
  editorHost.Children.Add(searchHistoryPanel);
  editorHost.Children.Add(editor);
  editor.TextChanged+=(_,_)=>{dirty=true;};
  searchBox.KeyDown+=(_,e)=>{if(e.Key==Key.Enter)Search(true);};
  outlineSearch.KeyDown+=(_,e)=>{if(e.Key==Key.Enter)SearchOutline();};
  Closed+=(_,_)=>{if(dirty&&MessageBox.Show(this,"هل تريد الحفظ قبل الإغلاق؟","محرر الأنصاري",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)SaveDoc();};
  status.Text="جاهز — أضف مستندًا أو ابدأ الكتابة";
 }
 Button LocalButton(Panel panel,string label,Action action){var b=new Button{Content=label,Margin=new Thickness(2),Padding=new Thickness(8,3,8,3),FontSize=12};panel.Children.Add(b);b.Click+=(_,_)=>{try{action();}catch(Exception e){status.Text=e.Message;}};return b;}
 void Capsule(Button b,string color){b.Background=(Brush)new BrushConverter().ConvertFromString(color)!;b.Foreground=Brushes.White;b.FontWeight=FontWeights.SemiBold;b.Cursor=Cursors.Hand;var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetBinding(ContentPresenter.ContentProperty,new Binding("Content"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});border.AppendChild(content);b.Template=new ControlTemplate(typeof(Button)){VisualTree=border};}
 void ApplyToSelection(Action<TextRange> action){var range=new TextRange(editor.Selection.Start,editor.Selection.End);if(!range.IsEmpty)action(range);}
 void ApplyToParagraph(Action<Paragraph> action){var p=editor.CaretPosition.Paragraph;if(p!=null)action(p);}
 void SetHeading(int level){var p=editor.CaretPosition.Paragraph;if(p==null)return;p.Tag=level;p.FontWeight=FontWeights.Bold;p.FontSize=22-level*2;BuildOutline();status.Text=$"تم تطبيق عنوان مستوى {level}";}
 void NewDoc(){if(dirty&&MessageBox.Show(this,"هل تريد الحفظ قبل إنشاء مستند جديد؟","مستند جديد",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)SaveDoc();editor.Document=new FlowDocument();currentPath=null;dirty=false;ClearHighlights();BuildOutline();status.Text="تم إنشاء مستند جديد";}
 void OpenDoc(){
  var d=new OpenFileDialog{Title="فتح مستند",Filter="كل الملفات المدعومة|*.docx;*.doc;*.txt;*.md;*.markdown|Word|*.docx;*.doc|نص|*.txt|Markdown|*.md;*.markdown"};
  if(d.ShowDialog(this)!=true)return;
  try{
   editor.Document=new FlowDocument();ClearHighlights();string ext=Path.GetExtension(d.FileName).ToLowerInvariant();
   if(ext==".docx")OpenDocx(d.FileName);
   else if(ext==".doc"||ext==".txt"||ext==".md"||ext==".markdown")OpenTextLike(d.FileName,ext);
   else throw new InvalidDataException("صيغة غير مدعومة");
   currentPath=d.FileName;dirty=false;BuildOutline();status.Text="تم فتح: "+Path.GetFileName(d.FileName);
  }catch(Exception e){status.Text="تعذر فتح الملف: "+e.Message;}
 }
 void OpenDocx(string path){
  using var doc=WordprocessingDocument.Open(path,false);
  var body=doc.MainDocumentPart?.Document?.Body??throw new InvalidDataException("ملف DOCX غير صالح");
  foreach(var p in body.Elements<W.Paragraph>()){
   var para=new Paragraph();
   var just=p.ParagraphProperties?.Justification?.Val;
   if(just==W.JustificationValues.Center)para.TextAlignment=TextAlignment.Center;else if(just==W.JustificationValues.Left)para.TextAlignment=TextAlignment.Left;else if(just==W.JustificationValues.Right)para.TextAlignment=TextAlignment.Right;else if(just==W.JustificationValues.Both)para.TextAlignment=TextAlignment.Justify;
   int level=0;var style=p.ParagraphProperties?.ParagraphStyleId?.Val?.Value??"";
   if(style.StartsWith("Heading",StringComparison.OrdinalIgnoreCase)&&int.TryParse(style.Substring(7),out var l))level=l;
   foreach(var r in p.Elements<W.Run>()){
    var run=new Run(r.InnerText);
    var rp=r.RunProperties;
    if(rp!=null){
     if(rp.Bold!=null)run.FontWeight=FontWeights.Bold;
     if(rp.Italic!=null)run.FontStyle=FontStyles.Italic;
     if(rp.Underline!=null)run.TextDecorations=TextDecorations.Underline;
     if(rp.Color!=null&&rp.Color.Val!=null)run.Foreground=(Brush)new BrushConverter().ConvertFromString("#"+rp.Color.Val.Value!.TrimStart('#'))!;
     if(rp.Size!=null&&int.TryParse(rp.Size.Val?.Value,out var half))run.FontSize=half/2.0;
    }
    para.Inlines.Add(run);
   }
   if(level>0){para.Tag=level;para.FontWeight=FontWeights.Bold;para.FontSize=22-level*2;}
   editor.Document.Blocks.Add(para);
  }
 }
 void OpenTextLike(string path,string ext){
  foreach(var line in Core.Read(path)){
   var para=new Paragraph();string text=line;
   int level=0;
   if(ext is ".md" or ".markdown"){int hashes=0;while(hashes<text.Length&&text[hashes]=='#')hashes++;if(hashes>0&&hashes<=6&&text.Length>hashes&&text[hashes]==' '){level=hashes;text=text[(hashes+1)..];}}
   if(level>0){para.Tag=level;para.FontWeight=FontWeights.Bold;para.FontSize=22-level*2;}
   para.Inlines.Add(new Run(text));editor.Document.Blocks.Add(para);
  }
 }
 void SaveDoc(){if(currentPath==null){SaveAsDoc();return;}SaveTo(currentPath);}
 void SaveAsDoc(){
  var d=new SaveFileDialog{Title="حفظ باسم",Filter="Word DOCX|*.docx|نص|*.txt|Markdown|*.md;*.markdown|PDF|*.pdf|Word DOC قديم|*.doc"};
  if(d.ShowDialog(this)!=true)return;
  try{SaveTo(d.FileName);currentPath=d.FileName;dirty=false;status.Text="تم الحفظ: "+Path.GetFileName(d.FileName);}catch(Exception e){status.Text="تعذر الحفظ: "+e.Message;}
 }
 void SaveTo(string path){
  string ext=Path.GetExtension(path).ToLowerInvariant();
  if(ext==".docx")SaveDocx(path);
  else if(ext==".txt")SavePlain(path,false);
  else if(ext is ".md" or ".markdown")SavePlain(path,true);
  else if(ext==".pdf")ExportPdfTo(path);
  else if(ext==".doc")throw new NotSupportedException("حفظ DOC القديم غير مدعوم حاليًا؛ احفظ بصيغة DOCX أو PDF");
  else throw new NotSupportedException("صيغة حفظ غير مدعومة");
 }
 void SavePlain(string path,bool markdown){
  var sb=new StringBuilder();
  foreach(var p in editor.Document.Blocks.OfType<Paragraph>()){
   var text=new TextRange(p.ContentStart,p.ContentEnd).Text.TrimEnd('\r','\n');
   int level=p.Tag is int l?l:0;
   if(markdown&&level>0)sb.Append(new string('#',level)).Append(' ');
   sb.AppendLine(text);
  }
  File.WriteAllText(path,sb.ToString(),new UTF8Encoding(false));
 }
 void SaveDocx(string path){
  using var doc=WordprocessingDocument.Create(path,WordprocessingDocumentType.Document);
  var main=doc.AddMainDocumentPart();main.Document=new W.Document(new W.Body());var body=main.Document.Body!;
  foreach(var p in editor.Document.Blocks.OfType<Paragraph>()){
   var wp=new W.Paragraph();var pp=new W.ParagraphProperties();
   if(p.TextAlignment==TextAlignment.Center)pp.Append(new W.Justification{Val=W.JustificationValues.Center});
   else if(p.TextAlignment==TextAlignment.Left)pp.Append(new W.Justification{Val=W.JustificationValues.Left});
   else if(p.TextAlignment==TextAlignment.Right)pp.Append(new W.Justification{Val=W.JustificationValues.Right});
   else if(p.TextAlignment==TextAlignment.Justify)pp.Append(new W.Justification{Val=W.JustificationValues.Both});
   if(pp.HasChildren)wp.Append(pp);
   foreach(var inline in p.Inlines)WriteInline(inline,wp);
   body.Append(wp);
  }
 }
 void WriteInline(Inline inline,W.Paragraph wp){
  if(inline is Run r){
   var run=new W.Run(new W.Text(r.Text){Space=SpaceProcessingModeValues.Preserve});
   var rp=new W.RunProperties();
   if(r.FontWeight==FontWeights.Bold)rp.Append(new W.Bold());
   if(r.FontStyle==FontStyles.Italic)rp.Append(new W.Italic());
   if(r.TextDecorations.Count>0)rp.Append(new W.Underline());
   if(r.Foreground is SolidColorBrush b)rp.Append(new W.Color{Val=b.Color.ToString()});
   if(rp.HasChildren)run.RunProperties=rp;
   wp.Append(run);
  }else if(inline is Span s){foreach(var c in s.Inlines)WriteInline(c,wp);}
 }
 void ExportPdf(){var d=new SaveFileDialog{Title="تصدير PDF",Filter="PDF|*.pdf",FileName=string.IsNullOrEmpty(currentPath)?"مستند.pdf":Path.GetFileNameWithoutExtension(currentPath)+".pdf"};if(d.ShowDialog(this)!=true)return;ExportPdfTo(d.FileName);status.Text="تم تصدير PDF: "+Path.GetFileName(d.FileName);}
 void ExportPdfTo(string path){
  QuestPDF.Settings.License=LicenseType.Community;
  var items=new List<(string Text,int Level)>();
  foreach(var p in editor.Document.Blocks.OfType<Paragraph>()){
   var text=new TextRange(p.ContentStart,p.ContentEnd).Text.TrimEnd('\r','\n');
   int level=p.Tag is int l?l:0;
   if(text.Length>0)items.Add((text,level));
  }
  Document.Create(c=>{c.Page(page=>{page.Margin(40);page.Content().Column(col=>{foreach(var (text,level) in items){col.Item().Text(text).FontSize(level>0?20-level*2:11);if(level>0)col.Item().Text(text).Bold();}});});}).GeneratePdf(path);
 }
 void ClearHighlights(){foreach(var r in highlightRanges)r.ApplyPropertyValue(TextElement.BackgroundProperty,Brushes.Transparent);highlightRanges.Clear();matches.Clear();currentMatch=-1;}
 void Search(bool forward){
  ClearHighlights();var terms=Core.Terms(searchBox.Text);if(terms.Length==0){status.Text="اكتب كلمات البحث";return;}
  foreach(var p in editor.Document.Blocks.OfType<Paragraph>()){
   var text=new TextRange(p.ContentStart,p.ContentEnd).Text;
   if(!terms.All(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase)))continue;
   var ranges=new List<TextRange>();
   foreach(var term in terms){int at=0;while(true){int idx=text.IndexOf(term,at,StringComparison.OrdinalIgnoreCase);if(idx<0)break;var start=p.ContentStart.GetPositionAtOffset(idx,LogicalDirection.Forward);var end=start.GetPositionAtOffset(term.Length,LogicalDirection.Forward);var tr=new TextRange(start,end);tr.ApplyPropertyValue(TextElement.BackgroundProperty,Brushes.LemonChiffon);highlightRanges.Add(tr);ranges.Add(tr);at=idx+term.Length;}}
   if(ranges.Count>0)matches.Add((p,ranges));
  }
  if(matches.Count==0){status.Text="لا توجد نتائج";return;}
  currentMatch=forward?0:matches.Count-1;ShowMatch(currentMatch);status.Text=$"{matches.Count} فقرة تحتوي كل الكلمات";
 }
 void Navigate(int direction){if(matches.Count==0){Search(direction>0);return;}if(currentMatch>=0)foreach(var r in matches[currentMatch].Ranges)r.ApplyPropertyValue(TextElement.BackgroundProperty,Brushes.LemonChiffon);currentMatch=(currentMatch+direction+matches.Count)%matches.Count;ShowMatch(currentMatch);}
 void ShowMatch(int index){var m=matches[index];foreach(var r in m.Ranges)r.ApplyPropertyValue(TextElement.BackgroundProperty,Brushes.Orange);editor.CaretPosition=m.Para.ContentStart;editor.Focus();}
 List<OutlineItem> CollectHeadings(){var list=new List<OutlineItem>();foreach(var p in editor.Document.Blocks.OfType<Paragraph>()){var text=new TextRange(p.ContentStart,p.ContentEnd).Text.Trim();int level=0;if(p.Tag is int l)level=l;else if(text.StartsWith('#'))level=text.TakeWhile(c=>c=='#').Count();if(level>0&&text.Length>0)list.Add(new OutlineItem(level,text,p));}return list;}
 void BuildOutline(){var items=CollectHeadings();BuildTree(items);}
 void BuildTree(IEnumerable<OutlineItem> items){
  outlineTree.Items.Clear();var stack=new List<TreeViewItem>();
  foreach(var h in items){
   var node=new TreeViewItem{Header=h.Text,Tag=h};
   while(stack.Count>h.Level-1)stack.RemoveAt(stack.Count-1);
   if(stack.Count==0)outlineTree.Items.Add(node);else stack[^1].Items.Add(node);
   stack.Add(node);
  }
 }
 void ToggleOutlineFold(){bool any=Walk(outlineTree.Items).Any(i=>i.IsExpanded);foreach(var i in Walk(outlineTree.Items))i.IsExpanded=!any;}
 IEnumerable<TreeViewItem> Walk(ItemCollection c){foreach(var o in c)if(o is TreeViewItem t){yield return t;foreach(var ch in Walk(t.Items))yield return ch;}}
 void SearchOutline(){var terms=Core.Terms(outlineSearch.Text);var all=CollectHeadings();var items=terms.Length==0?all:all.Where(h=>terms.All(t=>h.Text.Contains(t,StringComparison.OrdinalIgnoreCase))).ToList();BuildTree(items);status.Text=items.Count>0?$"نتائج المخطط: {items.Count}":"لا توجد عناوين مطابقة";}
 void SyncOutline(){if(outlineTree.SelectedItem is TreeViewItem node&&node.Tag is OutlineItem h){editor.CaretPosition=h.Paragraph.ElementStart;editor.Selection.Select(h.Paragraph.ContentStart,h.Paragraph.ContentEnd);editor.Focus();h.Paragraph.Background=Brushes.LightBlue;status.Text="تمت مزامنة العنوان: "+h.Text;}else status.Text="اختر عنوانًا من المخطط أولًا";}
 void TogglePanel(Border panel,StackPanel chips,string kind,TextBox box,Action<string> onUse){bool show=panel.Visibility!=Visibility.Visible;panel.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(show)RenderHistory(kind,chips,box,onUse);}
 void RenderHistory(string kind,StackPanel chips,TextBox box,Action<string> onUse){
  chips.Children.Clear();var items=LoadHistory(kind);
  if(items.Count==0){chips.Children.Add(new TextBlock{Text="لا يوجد سجل بعد",Margin=new Thickness(6),Foreground=Brushes.SaddleBrown});return;}
  foreach(var t in items){var b=new Button{Content=new TextBlock{Text=t,TextWrapping=TextWrapping.NoWrap},ToolTip=t,FontSize=11,Padding=new Thickness(6,2,6,2),Margin=new Thickness(2)};Capsule(b,"#8C4B16");b.Click+=(_,_)=>{box.Text=t;onUse(t);};chips.Children.Add(b);}
 }
 List<string> LoadHistory(string kind){var file=Path.Combine(baseDir,$"editor-history-{kind}.json");try{if(File.Exists(file))return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file))??new();}catch(Exception e){status.Text=e.Message;}return new();}
 void SaveHistory(string kind,string text){var value=text.Trim();if(value.Length==0)return;var items=LoadHistory(kind);items.RemoveAll(p=>p.Equals(value,StringComparison.OrdinalIgnoreCase));items.Insert(0,value);if(items.Count>50)items.RemoveRange(50,items.Count-50);Directory.CreateDirectory(baseDir);File.WriteAllText(Path.Combine(baseDir,$"editor-history-{kind}.json"),JsonSerializer.Serialize(items));}
}
