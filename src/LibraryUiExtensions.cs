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
 bool libraryUiInstalled,historyLocal,foldFiles;
 readonly List<string> searchHistory=new();
 readonly string searchHistoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ElansaryWordLibrary","search-history.json");
 readonly DispatcherTimer historySaveTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};
 string pendingHistory="";
 string? libraryRemovalPath;
 bool libraryRemovalFolder;
 StackPanel? historyChips;
 Border? historyPanel;
 Button? unifiedHistoryButton,foldToggleButton;
 static readonly string[] CapsuleColors={"#6A4FD1","#16754B","#2563A6","#B03333","#9A5600","#8C4B16","#7A3E9D","#1F6F8B","#A34700","#5B6B23"};
 [ModuleInitializer]
 internal static void RegisterLibraryUiExtensions(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{
   if(sender is LibraryWindow window&&ReferenceEquals(e.OriginalSource,window))window.InstallLibraryUiExtensions();
  }));
 }
 static void Capsule(Button button,string color){
  button.Background=(Brush)new BrushConverter().ConvertFromString(color)!;
  button.Foreground=Brushes.White;button.FontWeight=FontWeights.SemiBold;
  button.Padding=new Thickness(12,6,12,6);button.Margin=new Thickness(3);button.Cursor=Cursors.Hand;
  var border=new FrameworkElementFactory(typeof(Border));
  border.SetValue(Border.CornerRadiusProperty,new CornerRadius(18));
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
  libraryUiInstalled=true;LoadHistory();
  var folderBar=new StackPanel{Orientation=Orientation.Horizontal};
  foldToggleButton=LocalButton(folderBar,"طي الملفات",()=>{ToggleFoldFiles();return Task.CompletedTask;});
  var refresh=LocalButton(folderBar,"تحديث المجلدات",RefreshLibraryFolders);busyButtons.Add(refresh);
  var remove=LocalButton(folderBar,"حذف من المكتبة",()=>{RemoveSelectedLibraryItem();return Task.CompletedTask;});busyButtons.Add(remove);
  remove.ToolTip="إزالة الملف أو المجلد وفروعه من قائمة المكتبة فقط؛ لا حذف من القرص";
  var clear=libraryTop.Children.OfType<WrapPanel>().FirstOrDefault()?.Children.OfType<Button>().FirstOrDefault(b=>b.Content?.ToString()=="تفريغ المكتبة");
  if(clear!=null){var parent=clear.Parent as Panel;parent?.Children.Remove(clear);folderBar.Children.Add(clear);}
  Capsule(foldToggleButton,"#6A4FD1");Capsule(refresh,"#2563A6");Capsule(remove,"#B03333");if(clear!=null)Capsule(clear,"#7A3E9D");
  libraryTop.Children.Insert(3,folderBar);
  foreach(var old in tools.Children.OfType<Button>().Where(b=>b.Content?.ToString()=="طي / فرد ملفات المكتبة").ToArray()){tools.Children.Remove(old);busyButtons.Remove(old);}
  CapsuleAll(tools);CapsuleAll(viewerBar);
  library.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((_,e)=>CaptureLibraryTarget(e.OriginalSource as DependencyObject)),true);
  var historyArea=new StackPanel{Margin=new Thickness(0,4,0,4)};
  unifiedHistoryButton=LocalButton(historyArea,"سجل البحث الموحد — إظهار",()=>{ToggleHistory();return Task.CompletedTask;});
  unifiedHistoryButton.HorizontalAlignment=HorizontalAlignment.Right;Capsule(unifiedHistoryButton,"#9A5600");
  historyChips=new StackPanel{Orientation=Orientation.Horizontal};
  historyPanel=new Border{Visibility=Visibility.Collapsed,Background=Brushes.LemonChiffon,BorderBrush=Brushes.Goldenrod,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(6),Child=new ScrollViewer{Content=historyChips,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,MinHeight=48,MaxHeight=90}};
  historyArea.Children.Add(historyPanel);libraryTop.Children.Add(historyArea);
  historySaveTimer.Tick+=(_,_)=>{historySaveTimer.Stop();SaveQuery(pendingHistory,false);};
  void Track(TextBox box,bool local){
   box.GotKeyboardFocus+=(_,_)=>historyLocal=local;
   box.TextChanged+=(_,_)=>{pendingHistory=box.Text;historySaveTimer.Stop();historySaveTimer.Start();};
   box.LostKeyboardFocus+=(_,_)=>{historySaveTimer.Stop();SaveQuery(box.Text,local);};
   box.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){historySaveTimer.Stop();SaveQuery(box.Text,local);}};
  }
  Track(libraryNameFilter,false);Track(query,false);Track(inside,true);
  Closed+=(_,_)=>{historySaveTimer.Stop();SaveQuery(pendingHistory,false);};
  RenderHistory();Title="مكتبة وورد الأنصاري — أزرار كبسولية وسجل داخلي وطي مرحلي — مراجعة 11";
  if(!ReferenceEquals(folderBar.Parent,libraryTop)||libraryTop.Children.IndexOf(folderBar)!=3||folderBar.Children.OfType<Button>().Count()!=4||libraryTop.Children.IndexOf(historyArea)!=libraryTop.Children.Count-1||historyChips.Orientation!=Orientation.Horizontal||tools.Children.OfType<Button>().Any(b=>b.Content?.ToString()=="طي / فرد ملفات المكتبة"))throw new InvalidOperationException("Library toolbar/history integration regression");
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
 void SaveQuery(string text,bool local){
  string value=text.Trim();if(value.Length==0)return;
  if(searchHistory.Count>0&&searchHistory[0].Equals(value,StringComparison.Ordinal))return;
  searchHistory.RemoveAll(p=>p.Equals(value,StringComparison.OrdinalIgnoreCase));searchHistory.Insert(0,value);if(searchHistory.Count>50)searchHistory.RemoveRange(50,searchHistory.Count-50);
  try{WriteLibraryJson(searchHistoryPath,searchHistory);}catch(Exception e){errors.Add(e.ToString());status.Text="تعذر حفظ السجل: "+e.Message;}RenderHistory();
 }
 void LoadHistory(){
  try{if(File.Exists(searchHistoryPath))searchHistory.AddRange((JsonSerializer.Deserialize<string[]>(File.ReadAllText(searchHistoryPath))??Array.Empty<string>()).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(50));}catch(Exception e){errors.Add(e.ToString());}
 }
 void ToggleHistory(){if(historyPanel==null||unifiedHistoryButton==null)return;bool show=historyPanel.Visibility!=Visibility.Visible;historyPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;unifiedHistoryButton.Content=show?"سجل البحث الموحد — إخفاء":"سجل البحث الموحد — إظهار";}
 void RenderHistory(){
  if(historyChips==null)return;historyChips.Children.Clear();
  if(searchHistory.Count==0){historyChips.Children.Add(new TextBlock{Text="السجل مشترك لبحث المجلدات والبحث العام والبحث داخل الملف؛ اكتب في أي خانة لحفظه",Margin=new Thickness(8),Foreground=Brushes.SaddleBrown});return;}
  foreach(string term in searchHistory){
   var chip=new Button{Content=new TextBlock{Text=term,TextWrapping=TextWrapping.NoWrap},ToolTip=term};Capsule(chip,"#8C4B16");
   chip.Click+=async(_,_)=>{try{bool local=historyLocal;var target=local?inside:query;target.Text=term;target.CaretIndex=term.Length;SaveQuery(term,local);if(local)await Search(true);else if(ReferenceEquals(target,libraryNameFilter)){libraryNameFilter.TextChanged-=null;libraryFilterTimer.Stop();QueueLibraryRefresh(false);}else await Search(false);}catch(Exception e){errors.Add(e.ToString());status.Text=e.Message;}};
   historyChips.Children.Add(chip);
  }
 }
}
