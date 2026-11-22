using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Ansary;
public sealed partial class LibraryWindow {
 readonly Dictionary<string,Dictionary<int,int>> markdownHeadingCache=new(StringComparer.OrdinalIgnoreCase);
 [ModuleInitializer]
 internal static void RegisterMarkdownNoteFixes(){
  EventManager.RegisterClassHandler(typeof(LibraryWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{if(sender is LibraryWindow window&&ReferenceEquals(e.OriginalSource,window))window.FixNoteSpace();}));
  EventManager.RegisterClassHandler(typeof(DocumentParagraphView),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=>{if(sender is DocumentParagraphView view&&ReferenceEquals(e.OriginalSource,view))view.Owner?.StyleMarkdownHeading(view);}));
 }
 void FixNoteSpace(){
  foreach(var text in new[]{popupNoteText,bottomNoteText}){text.MaxHeight=420;text.MinHeight=140;text.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;text.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;text.TextWrapping=TextWrapping.Wrap;text.VerticalContentAlignment=VerticalAlignment.Top;text.TextChanged+=(_,_)=>text.ScrollToHome();}
  notePanel.MaxHeight=440;
  if(Environment.GetCommandLineArgs().Contains("--startup-smoke-test")){TextFormats.Verify();if(bottomNoteText.MaxHeight!=420||popupNoteText.MinHeight!=140)throw new InvalidOperationException("Footnote layout regression");}
 }
 void StyleMarkdownHeading(DocumentParagraphView view){
  if(displayed is not DocumentSnapshot doc||!TextFormats.Markdown(doc.Path)||view.Row is not DisplayRow row||view.Child is not HighlightBlock body)return;
  string key=doc.Path+"|"+doc.Length+"|"+doc.Ticks;
  if(!markdownHeadingCache.TryGetValue(key,out var headings)){try{headings=TextFormats.Headings(doc.Path).ToDictionary(h=>h.Index,h=>h.Level);if(markdownHeadingCache.Count>8)markdownHeadingCache.Clear();markdownHeadingCache[key]=headings;}catch(Exception e){errors.Add(e.ToString());return;}}
  int index=ReadingParagraphIndex(view,row);
  if(headings.TryGetValue(index,out int level)){body.FontWeight=FontWeights.Bold;body.FontSize=readingOptions.TextSize+Math.Max(2,8-level);body.LineHeight=body.FontSize*readingOptions.LineFactor;}else body.FontWeight=FontWeights.Normal;
 }
}
