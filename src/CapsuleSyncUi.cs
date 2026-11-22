using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace Ansary;
public sealed partial class LibraryWindow {
 bool capsuleSyncInstalled;
 CheckBox? capsuleMarkedFile;
 Brush? capsulePreviousFileBrush;
 [ModuleInitializer]
 internal static void RegisterCapsuleSyncUi(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{
   if(sender is LibraryWindow window && ReferenceEquals(e.OriginalSource,window))window.InstallCapsuleSyncUi();
  }));
 }
 static Brush CapsuleGradient(string top,string bottom)=>new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),(Color)ColorConverter.ConvertFromString(bottom),90);
 void InstallCapsuleSyncUi(){
  if(capsuleSyncInstalled)return;
  if(library.Parent is not Grid grid || !ReferenceEquals(results.Parent,grid)){status.Text="تعذر إضافة أدوات المزامنة: تخطيط الواجهة غير متوقع";return;}
  capsuleSyncInstalled=true;
  var frame=new FrameworkElementFactory(typeof(Border),"CapsuleFrame");
  frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(20));
  frame.SetValue(Border.BorderThicknessProperty,new Thickness(2));
  frame.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});
  frame.SetBinding(Border.BorderBrushProperty,new Binding("BorderBrush"){RelativeSource=RelativeSource.TemplatedParent});
  frame.SetBinding(Border.PaddingProperty,new Binding("Padding"){RelativeSource=RelativeSource.TemplatedParent});
  var content=new FrameworkElementFactory(typeof(ContentPresenter));
  content.SetValue(ContentPresenter.ContentSourceProperty,"Content");
  content.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);
  content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
  content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
  frame.AppendChild(content);
  var template=new ControlTemplate(typeof(Button)){VisualTree=frame};
  var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};
  hover.Setters.Add(new Setter(UIElement.OpacityProperty,0.85,"CapsuleFrame"));template.Triggers.Add(hover);
  var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};
  pressed.Setters.Add(new Setter(UIElement.OpacityProperty,0.65,"CapsuleFrame"));template.Triggers.Add(pressed);
  var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};
  focus.Setters.Add(new Setter(Border.BorderBrushProperty,Brushes.Gold,"CapsuleFrame"));template.Triggers.Add(focus);
  var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};
  disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.45,"CapsuleFrame"));template.Triggers.Add(disabled);
  var style=new Style(typeof(Button));
  style.Setters.Add(new Setter(Control.TemplateProperty,template));
  style.Setters.Add(new Setter(Control.BackgroundProperty,CapsuleGradient("#245C96","#173D67")));
  style.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));
  style.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));
  style.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.SemiBold));
  style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,36d));
  style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,72d));
  style.Setters.Add(new Setter(FrameworkElement.CursorProperty,Cursors.Hand));
  Resources[typeof(Button)]=style;
  var fileBar=new WrapPanel();
  var fileButton=LocalButton(fileBar,"مزامنة الملف المفتوح",()=>{CapsuleLocateFile();return System.Threading.Tasks.Task.CompletedTask;});
  fileButton.Background=CapsuleGradient("#186C65","#104A45");
  fileButton.ToolTip="إظهار الملف المفتوح وتمييزه دون تغيير ملفات البحث المحددة";
  CapsuleWrapPanel(grid,library,fileBar);
  var resultBar=new WrapPanel();
  var resultButton=LocalButton(resultBar,"مزامنة النتيجة المفتوحة",()=>{CapsuleLocateResult();return System.Threading.Tasks.Task.CompletedTask;});
  resultButton.Background=CapsuleGradient("#704698","#4D2E6D");
  resultButton.ToolTip="إظهار الفقرة المفتوحة داخل نتائج البحث العام دون إعادة البحث";
  CapsuleWrapPanel(grid,results,resultBar);
  var selectedFrame=new FrameworkElementFactory(typeof(Border),"SyncedResultFrame");
  selectedFrame.SetValue(Border.BorderThicknessProperty,new Thickness(3));
  selectedFrame.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
  var rowText=new FrameworkElementFactory(typeof(HighlightBlock));rowText.SetBinding(HighlightBlock.RowProperty,new Binding());selectedFrame.AppendChild(rowText);
  var rowTemplate=new DataTemplate{VisualTree=selectedFrame};
  var selectedTrigger=new DataTrigger{Binding=new Binding("IsSelected"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1)},Value=true};
  selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty,Brushes.DarkMagenta,"SyncedResultFrame"));rowTemplate.Triggers.Add(selectedTrigger);results.ItemTemplate=rowTemplate;
 }
 static void CapsuleWrapPanel(Grid grid,FrameworkElement list,Panel bar){
  int column=Grid.GetColumn(list),row=Grid.GetRow(list),columnSpan=Grid.GetColumnSpan(list),rowSpan=Grid.GetRowSpan(list);
  grid.Children.Remove(list);
  var host=new DockPanel();Grid.SetColumn(host,column);Grid.SetRow(host,row);Grid.SetColumnSpan(host,columnSpan);Grid.SetRowSpan(host,rowSpan);
  DockPanel.SetDock(bar,Dock.Top);host.Children.Add(bar);host.Children.Add(list);grid.Children.Add(host);
 }
 void CapsuleLocateFile(){
  var hit=current;if(hit==null){status.Text="افتح ملفًا أولًا";return;}
  foreach(var folder in library.Items.OfType<TreeViewItem>()){
   var box=folder.Items.OfType<CheckBox>().FirstOrDefault(b=>string.Equals(b.ToolTip?.ToString(),hit.Path,StringComparison.OrdinalIgnoreCase));
   if(box==null)continue;
   if(capsuleMarkedFile!=null)capsuleMarkedFile.Background=capsulePreviousFileBrush;
   capsuleMarkedFile=box;capsulePreviousFileBrush=box.Background;box.Background=Brushes.Gold;
   folder.IsExpanded=true;library.UpdateLayout();
   if(folder.ItemContainerGenerator.ContainerFromItem(box) is TreeViewItem container){container.IsSelected=true;container.BringIntoView();}
   box.BringIntoView();status.Text="تم إظهار الملف المفتوح: "+Path.GetFileName(hit.Path);return;
  }
  status.Text="الملف المفتوح غير موجود في لوحة المجلدات";
 }
 void CapsuleLocateResult(){
  var hit=current;if(hit==null){status.Text="افتح نتيجة أولًا";return;}
  foreach(var group in groups){
   var target=AllRows(group).FirstOrDefault(r=>r.Hit is SearchHit h && string.Equals(h.Path,hit.Path,StringComparison.OrdinalIgnoreCase) && h.Index==hit.Index && h.Length==hit.Length && h.Ticks==hit.Ticks && h.Text==hit.Text);
   if(target==null)continue;
   foreach(var ancestor in AllRows(group))if(Contains(ancestor,target))ancestor.Open=true;
   RefreshResults();results.SelectedItem=target;results.ScrollIntoView(target);
   status.Text="تم إظهار النتيجة في البحث العام: "+Path.GetFileName(hit.Path)+" — فقرة "+(hit.Index+1);return;
  }
  status.Text="الفقرة المفتوحة ليست ضمن نتائج البحث العام الحالي؛ لم يُعَد البحث";
 }
}
