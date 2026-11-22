using System.IO;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace Ansary;
public sealed partial class LibraryWindow {
 bool libraryUiInstalled,foldFiles;
 string? libraryRemovalPath;
 bool libraryRemovalFolder;
 Button? foldToggleButton;
 static readonly string[] CapsuleColors={"#6A4FD1","#16754B","#2563A6","#B03333","#9A5600","#8C4B16","#7A3E9D","#1F6F8B","#A34700","#5B6B23"};
 [ModuleInitializer]
 internal static void RegisterLibraryUiExtensions(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{
   if(sender is LibraryWindow window&&ReferenceEquals(e.OriginalSource,window))window.InstallLibraryUiExtensions();
  }));
 }
 static void Capsule(Button button,string color){
  button.Background=(Brush)new BrushConverter().ConvertFromString(color)!;
  button.Foreground=Brushes.White;button.FontWeight=FontWeights.SemiBold;button.FontSize=12;
  button.Padding=new Thickness(8,3,8,3);button.Margin=new Thickness(2);button.Cursor=Cursors.Hand;
  var border=new FrameworkElementFactory(typeof(Border));
  border.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));
  border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});
  var content=new FrameworkElementFactory(typeof(ContentPresenter));
  content.SetBinding(ContentPresenter.ContentProperty,new Binding("Content"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});
  content.SetBinding(FrameworkElement.MarginProperty,new Binding("Padding"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});
  content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
  border.AppendChild(content);button.Template=new ControlTemplate(typeof(Button)){VisualTree=border};
 }
 void CapsuleAll(Panel panel,int start=0){int index=start;foreach(var child in panel.Children){if(child is Button button){Capsule(button,CapsuleColors[index++%CapsuleColors.Length]);}}}
 void InstallLibraryUiExtensions(){
  if(libraryUiInstalled)return;
  if(library.Parent is not DockPanel host||host.Children.OfType<StackPanel>().FirstOrDefault() is not StackPanel libraryTop||Content is not DockPanel root)throw new InvalidOperationException("Library toolbar host missing");
  var mainContent=root.Children.OfType<Grid>().FirstOrDefault()??throw new InvalidOperationException("Main library grid missing");
  libraryUiInstalled=true;
  var folderBar=new WrapPanel();
  foldToggleButton=LocalButton(folderBar,"طي الملفات",()=>{ToggleFoldFiles();return Task.CompletedTask;});
  var refresh=LocalButton(folderBar,"تحديث المجلدات",RefreshLibraryFolders);busyButtons.Add(refresh);
  var remove=LocalButton(folderBar,"حذف من المكتبة",()=>{RemoveSelectedLibraryItem();return Task.CompletedTask;});busyButtons.Add(remove);
  remove.ToolTip="إزالة الملف أو المجلد وفروعه من قائمة المكتبة فقط؛ لا حذف من القرص";
  var actions=libraryTop.Children.OfType<WrapPanel>().FirstOrDefault()??throw new InvalidOperationException("Library actions missing");
  var clear=actions.Children.OfType<Button>().FirstOrDefault(b=>b.Content?.ToString()=="تفريغ المكتبة")??throw new InvalidOperationException("Clear library button missing");
  actions.Children.Remove(clear);folderBar.Children.Add(clear);
  Capsule(foldToggleButton,"#6A4FD1");Capsule(refresh,"#2563A6");Capsule(remove,"#B03333");Capsule(clear,"#7A3E9D");
  libraryTop.Children.Insert(3,folderBar);
  CapsuleAll(actions,4);
  foreach(var old in tools.Children.OfType<Button>().Where(b=>b.Content?.ToString()=="طي / فرد ملفات المكتبة").ToArray()){tools.Children.Remove(old);busyButtons.Remove(old);}
  CapsuleAll(tools);CapsuleAll(viewerBar);
  library.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((_,e)=>CaptureLibraryTarget(e.OriginalSource as DependencyObject)),true);
  AttachHistory(libraryNameFilter,libraryTop,false);
  AttachHistory(query,tools,false);
  AttachHistory(inside,viewerBar,true);
  Title="مكتبة وورد الأنصاري — أزرار كبسولية وسجل لكل بحث وطي مرحلي — مراجعة 12";
  if(!ReferenceEquals(folderBar.Parent,libraryTop)||libraryTop.Children.IndexOf(folderBar)!=3||folderBar.Children.OfType<Button>().Count()!=4||tools.Children.OfType<Button>().Any(b=>b.Content?.ToString()=="طي / فرد ملفات المكتبة"))throw new InvalidOperationException("Library toolbar integration regression");
 }
 void AttachHistory(TextBox box,Panel container,bool local){
  int index=container.Children.IndexOf(box);if(index<0)throw new InvalidOperationException("Search box host missing");
  var host=new StackPanel{Orientation=Orientation.Vertical};
  container.Children.RemoveAt(index);container.Children.Insert(index,host);
  var row=new StackPanel{Orientation=Orientation.Horizontal};
  var historyButton=new Button{Content="سجل",FontSize=11,Padding=new Thickness(6,2,6,2),Margin=new Thickness(2)};
  var chips=new StackPanel{Orientation=Orientation.Horizontal};
  var historyPanel=new Border{Visibility=Visibility.Collapsed,Background=Brushes.LemonChiffon,BorderBrush=Brushes.Goldenrod,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(4),Margin=new Thickness(0,2,0,2),Child=new ScrollViewer{Content=chips,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,MaxHeight=70}};
  row.Children.Add(box);row.Children.Add(historyButton);host.Children.Add(row);host.Children.Add(historyPanel);
  Capsule(historyButton,"#8C4B16");
  var history=new SearchBoxHistory(box,local,chips,historyPanel);
  historyButton.Click+=(_,_)=>{bool show=historyPanel.Visibility!=Visibility.Visible;historyPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(show)history.Render();};
  box.TextChanged+=(_,_)=>history.QueueSave(box.Text);
  box.LostKeyboardFocus+=(_,_)=>history.SaveNow(box.Text);
  box.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter)history.SaveNow(box.Text);};
  Closed+=(_,_)=>history.SaveNow(box.Text);
  history.Load();history.Render();
 }
 void ToggleFoldFiles(){
  foldFiles=!foldFiles;
  if(foldToggleButton!=null)foldToggleButton.Content=foldFiles?"فرد الملفات":"طي الملفات";
  FoldLibraryFiles(!foldFiles);
 }
 void FoldLibraryFiles(bool open){
  foreach(var entry in libraryNodes){
   var node=entry.Value;
   bool hasFiles=node.Items.OfType<CheckBox>().Any();
   if(open)node.IsExpanded=true;
   else if(hasFiles)node.IsExpanded=false;
   libraryExpansion[entry.Key]=node.IsExpanded;
  }
  status.Text=open?"تم فرد جميع الملفات تحت جميع المجلدات":"تم طي الملفات تحت المجلدات؛ جميع المجلدات والمجلدات الفرعية ظاهرة";
 }
 async Task RefreshLibraryFolders(){
  var roots=files.Select(Path.GetDirectoryName).OfType<string>().Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p.Length).ToArray();
  var minimal=new List<string>();
  foreach(var path in roots)if(!minimal.Any(parent=>IsLibraryDescendant(path,parent)))minimal.Add(path);
  if(minimal.Count==0){status.Text="أضف مجلدًا أو ملفًا للمكتبة أولًا";return;}
  await Work(async token=>{
   status.Text="جار التقاط المستندات الجديدة دون تفريغ المكتبة…";
   var found=await Task.Run(()=>{var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var failures=new List<string>();foreach(var path in minimal){token.ThrowIfCancellationRequested();var scan=Core.Scan(path,token);paths.UnionWith(scan.Files);failures.AddRange(scan.Errors);}return(Paths:paths,Errors:failures);},token);
   token.ThrowIfCancellationRequested();errors.AddRange(found.Errors);
   var existing=files.ToHashSet(StringComparer.OrdinalIgnoreCase);bool customScope=selected.Count>0;int added=0;
   foreach(var path in found.Paths)if(existing.Add(path)){files.Add(path);if(customScope)selected.Add(path);added++;}
   if(added>0)SaveLibrary();QueueLibraryRefresh(true);status.Text=$"تم تحديث المكتبة: {added} مستند جديد — أخطاء القراءة: {found.Errors.Count} — لم تُفرغ المكتبة";
  });
 }
 static bool IsLibraryDescendant(string path,string folder){
  string full=Path.GetFullPath(path),root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
  return full.Equals(root,StringComparison.OrdinalIgnoreCase)||full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 }
 void CaptureLibraryTarget(DependencyObject? source){
  while(source!=null){
   if(source is CheckBox box&&box.ToolTip is string path){libraryRemovalPath=path;libraryRemovalFolder=!visibleLibraryFiles.ContainsKey(path);return;}
   if(source is TreeViewItem node&&node.Header is CheckBox header&&header.ToolTip is string folder){libraryRemovalPath=folder;libraryRemovalFolder=true;return;}
   source=source is FrameworkContentElement c?c.Parent:source is Visual?VisualTreeHelper.GetParent(source):(source as FrameworkElement)?.Parent;
  }
 }
 void RemoveSelectedLibraryItem(){
  if(cancel!=null){status.Text="انتظر اكتمال العملية الحالية أولًا";return;}
  string? path=libraryRemovalPath;bool folder=libraryRemovalFolder;
  if(path==null){if(library.SelectedItem is CheckBox file&&file.ToolTip is string p){path=p;folder=false;}else if(library.SelectedItem is TreeViewItem node&&node.Header is CheckBox box&&box.ToolTip is string p2){path=p2;folder=true;}}
  if(path==null){status.Text="اضغط اسم مجلد أو ملف في لوحة المكتبة أولًا";return;}
  string[] removed=files.Where(p=>folder?IsLibraryDescendant(p,path):p.Equals(path,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(removed.Length==0){status.Text="العنصر لم يعد موجودًا في قائمة المكتبة";return;}
  if(MessageBox.Show(this,$"إزالة {removed.Length} مستندًا من قائمة المكتبة فقط؟\n{path}\nلن يُحذف أي ملف أو مجلد من القرص.","إزالة من المكتبة فقط",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  var excluded=removed.ToHashSet(StringComparer.OrdinalIgnoreCase);var kept=files.Where(p=>!excluded.Contains(p)).ToArray();
  try{WriteLibraryJson(settings,kept);}catch(Exception e){errors.Add(e.ToString());status.Text="لم تُحذف عناصر من المكتبة؛ تعذر حفظ القائمة: "+e.Message;return;}
  files.Clear();files.AddRange(kept);selected.ExceptWith(excluded);libraryRemovalPath=null;QueueLibraryRefresh(true);
  status.Text=$"تمت إزالة {removed.Length} مستندًا من المكتبة فقط؛ جميع الملفات الأصلية باقية على القرص";
 }
 static void WriteLibraryJson(string target,IEnumerable<string> values){
  Directory.CreateDirectory(Path.GetDirectoryName(target)!);string temporary=target+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(values.ToArray()));File.Move(temporary,target,true);
 }
 void SaveLibrary()=>WriteLibraryJson(settings,files);
 sealed class SearchBoxHistory {
  readonly TextBox box;readonly bool local;readonly StackPanel chips;readonly Border panel;
  readonly List<string> items=new();readonly string file;
  readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};
  string pending="";
  public SearchBoxHistory(TextBox box,bool local,StackPanel chips,Border panel){
   this.box=box;this.local=local;this.chips=chips;this.panel=panel;
   string kind=ReferenceEquals(box,((LibraryWindow?)null)?.libraryNameFilter)?"folder":local?"inside":"general";
   file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ElansaryWordLibrary",$"search-history-{kind}.json");
   saveTimer.Tick+=(_,_)=>{saveTimer.Stop();SaveNow(pending);};
  }
  public void QueueSave(string text){pending=text;saveTimer.Stop();saveTimer.Start();}
  public void SaveNow(string text){
   saveTimer.Stop();string value=text.Trim();if(value.Length==0)return;
   if(items.Count>0&&items[0].Equals(value,StringComparison.Ordinal))return;
   items.RemoveAll(p=>p.Equals(value,StringComparison.OrdinalIgnoreCase));items.Insert(0,value);if(items.Count>50)items.RemoveRange(50,items.Count-50);
   try{WriteHistory();}catch(Exception e){((LibraryWindow?)null)?.errors.Add(e.ToString());}
   Render();
  }
  public void Load(){
   try{if(File.Exists(file))items.AddRange((JsonSerializer.Deserialize<string[]>(File.ReadAllText(file))??Array.Empty<string>()).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(50));}catch(Exception e){((LibraryWindow?)null)?.errors.Add(e.ToString());}
  }
  void WriteHistory(){Directory.CreateDirectory(Path.GetDirectoryName(file)!);string temporary=file+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(items.ToArray()));File.Move(temporary,file,true);}
  public void Render(){
   chips.Children.Clear();
   if(items.Count==0){chips.Children.Add(new TextBlock{Text="لا يوجد سجل بعد",Margin=new Thickness(6),Foreground=Brushes.SaddleBrown});return;}
   foreach(string term in items){
    var chip=new Button{Content=new TextBlock{Text=term,TextWrapping=TextWrapping.NoWrap},ToolTip=term,FontSize=11,Padding=new Thickness(6,2,6,2),Margin=new Thickness(2)};
    chip.Click+=async(_,_)=>{
     try{
      box.Text=term;box.CaretIndex=term.Length;SaveNow(term);
      if(local)await ((LibraryWindow?)null)!.Search(true);
      else if(ReferenceEquals(box,((LibraryWindow?)null)!.libraryNameFilter)){((LibraryWindow?)null)!.libraryFilterTimer.Stop();((LibraryWindow?)null)!.QueueLibraryRefresh(false);}
      else await ((LibraryWindow?)null)!.Search(false);
     }catch(Exception e){((LibraryWindow?)null)?.errors.Add(e.ToString());}
    };
    chips.Children.Add(chip);
   }
  }
 }
}
