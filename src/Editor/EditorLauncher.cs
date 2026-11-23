using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
namespace Ansary;
public static class EditorLauncher {
 [ModuleInitializer]
 internal static void Init(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,e)=>{if(s is LibraryWindow w&&ReferenceEquals(e.OriginalSource,w))Install(w);}));
 }
 static void Install(LibraryWindow w){
  var tools=(WrapPanel?)typeof(LibraryWindow).GetField("tools",BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(w);
  if(tools==null||tools.Children.OfType<Button>().Any(b=>b.Content?.ToString()=="محرر وعارض الأنصاري"))return;
  var b=new Button{Content="محرر وعارض الأنصاري",Margin=new Thickness(3),Padding=new Thickness(6)};
  b.Click+=(_,_)=>{var win=new Ansary.Editor.ElansaryEditorWindow();win.Show();};
  tools.Children.Add(b);
 }
}
