using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace Ansary;
public sealed partial class LibraryWindow {
 bool libraryUiInstalled;
 readonly List<string> searchHistory=new();
 readonly string historyPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ElansaryWordLibrary","search-history.json");
 WrapPanel? historyChips;
 Border? historyPanel;
 [ModuleInitializer]
 internal static void RegisterLibraryUiExtensions(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{
   if(sender is LibraryWindow window && ReferenceEquals(e.OriginalSource,window))window.InstallLibraryUiExtensions();
  }));
 }
 void InstallLibraryUiExtensions(){
  if(libraryUiInstalled)return;
  if(library.Parent is not Grid grid || !ReferenceEquals(results.Parent,grid)){status.Text="تعذر إضافة أدوات المكتبة: تخطيط الواجهة غير متوقع";return;}
  libraryUiInstalled=true;
  LoadHistory();
  var folderBar=new WrapPanel();
  var collapse=LocalButton(folderBar,"طي الملفات",()=>{FoldLibraryFiles(false);return System.Threading.Tasks.Task.CompletedTask;});
  var expand=LocalButton(folderBar,"فرد الملفات",()=>{FoldLibraryFiles(true);return System.Threading.Tasks.Task.CompletedTask;});
  var refresh=LocalButton(folderBar,"تحديث المجلدات",()=>RefreshLibraryFolders());
  var remove=LocalButton(folderBar,"حذف من المكتبة",()=>RemoveSelectedLibraryItem());
  collapse.Background=LibraryBrush("#8E7BEF","#6A4FD1");expand.Background=LibraryBrush("#35B37E","#1F8A5F");refresh.Background=LibraryBrush("#4F8EF7","#2F6FE0");remove.Background=LibraryBrush("#E05757","#B03A3A");
  WrapLibraryPanel(grid,library,folderBar);
  var historyBar=new DockPanel{Margin=new Thickness(0,4,0,4)};
  var historyButton=LocalButton(historyBar,"سجل البحث",()=>ToggleHistory());
  historyButton.Background=LibraryBrush("#F2B33D","#C98A20");DockPanel.SetDock(historyButton,Dock.Left);
  historyPanel=new Border{Visibility=Visibility.Collapsed,Background=Brushes.White,BorderBrush=Brushes.Goldenrod,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(8),MaxHeight=110};
  historyChips=new WrapPanel{Orientation=Orientation.Horizontal};historyPanel.Child=historyChips;
  DockPanel.SetDock(historyPanel,Dock.Top);historyBar.Children.Add(historyPanel);
  var root=Content as DockPanel;if(root==null){status.Text="تعذر إضافة سجل البحث";return;}
  DockPanel.SetDock(historyBar,Dock.Top);root.Children.Add(historyBar);
  query.LostKeyboardFocus+=(_,_)=>SaveQuery(query.Text,false);
  inside.LostKeyboardFocus+=(_,_)=>SaveQuery(inside.Text,true);
  query.KeyDown+=async(_,e)=>{if(e.Key==Key.Enter){SaveQuery(query.Text,false);await Search(false);}};
  inside.KeyDown+=async(_,e)=>{if(e.Key==Key.Enter){SaveQuery(inside.Text,true);await Search(true);}};
  RenderHistory();
 }
 static Brush LibraryBrush(string top,string bottom)=>new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),(Color)ColorConverter.ConvertFromString(bottom),90);
 static void WrapLibraryPanel(Grid grid,FrameworkElement list,Panel bar){
  int column=Grid.GetColumn(list),row=Grid.GetRow(list),columnSpan=Grid.GetColumnSpan(list),rowSpan=Grid.GetRowSpan(list);
  grid.Children.Remove(list);
  var host=new DockPanel();Grid.SetColumn(host,column);Grid.SetRow(host,row);Grid.SetColumnSpan(host,columnSpan);Grid.SetRowSpan(host,rowSpan);
  DockPanel.SetDock(bar,Dock.Top);host.Children.Add(bar);host.Children.Add(list);grid.Children.Add(host);
 }
 void FoldLibraryFiles(bool open){
  foreach(var folder in library.Items.OfType<TreeViewItem>())folder.IsExpanded=open;
  status.Text=open?"تم فرد جميع الملفات تحت المجلدات":"تم طي الملفات تحت المجلدات";
 }
 void RefreshLibraryFolders(){
  var existing=files.ToHashSet(StringComparer.OrdinalIgnoreCase);
  var roots=files.Select(Path.GetDirectoryName).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var added=0;
  foreach(var root in roots){
   try{foreach(var file in Directory.EnumerateFiles(root,"*.docx",SearchOption.AllDirectories)){if(existing.Add(file)){files.Add(file);selected.Add(file);added++;}}}catch(Exception e){errors.Add(e.Message);}
  }
  if(added>0)SaveLibrary();Rebuild();status.Text=$"تم تحديث المكتبة: أُضيف {added} ملفًا جديدًا";
 }
 void RemoveSelectedLibraryItem(){
  if(library.SelectedItem is TreeViewItem folder){
   var folderPath=folder.Header?.ToString();if(string.IsNullOrWhiteSpace(folderPath)){status.Text="اختر مجلدًا أولًا";return;}
   var removed=files.RemoveAll(p=>string.Equals(Path.GetDirectoryName(p),folderPath,StringComparison.OrdinalIgnoreCase));
   foreach(var p in files.ToList())if(string.Equals(Path.GetDirectoryName(p),folderPath,StringComparison.OrdinalIgnoreCase))RemoveLibraryPath(p);
   SaveLibrary();Rebuild();status.Text=$"تم حذف {removed} ملفًا من المكتبة فقط";return;
  }
  if(library.SelectedItem is CheckBox file){
   var path=file.ToolTip?.ToString();if(string.IsNullOrWhiteSpace(path)){status.Text="اختر ملفًا أولًا";return;}
   RemoveLibraryPath(path);SaveLibrary();Rebuild();status.Text=$"تم حذف الملف من المكتبة فقط: {Path.GetFileName(path)}";return;
  }
  status.Text="اختر مجلدًا أو ملفًا أولًا";
 }
 void RemoveLibraryPath(string path){files.RemoveAll(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));selected.Remove(path);}
 void SaveLibrary(){Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,JsonSerializer.Serialize(files));}
 void SaveQuery(string text,bool local){
  var value=text.Trim();if(value.Length==0)return;
  searchHistory.RemoveAll(p=>string.Equals(p,value,StringComparison.OrdinalIgnoreCase));
  searchHistory.Insert(0,value);if(searchHistory.Count>50)searchHistory.RemoveRange(50,searchHistory.Count-50);
  Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);File.WriteAllText(historyPath,JsonSerializer.Serialize(searchHistory));RenderHistory();
 }
 void LoadHistory(){
  try{if(File.Exists(historyPath))searchHistory.AddRange(JsonSerializer.Deserialize<string[]>(File.ReadAllText(historyPath))??Array.Empty<string>());}catch(Exception e){errors.Add(e.Message);}
 }
 void ToggleHistory(){
  if(historyPanel==null)return;
  historyPanel.Visibility=historyPanel.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
  if(historyPanel.Visibility==Visibility.Visible)RenderHistory();
 }
 void RenderHistory(){
  if(historyChips==null)return;historyChips.Children.Clear();
  foreach(var item in searchHistory.Take(50)){
   var chip=new Button{Content=item,Background=LibraryBrush("#FFF3C4","#E8C15A"),Foreground=Brushes.Black,FontWeight=FontWeights.SemiBold,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(3),Padding=new Thickness(10,5,10,5),Cursor=Cursors.Hand};
   chip.Click+=async(_,_)=>{query.Text=item;inside.Text=item;await Search(false);};
   historyChips.Children.Add(chip);
  }
 }
}
