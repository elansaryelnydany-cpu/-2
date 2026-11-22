using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace Ansary;
public sealed partial class LibraryWindow {
 void InitFormatControls(){Title="الأنصاري — DOC وDOCX ونص ومارك داون — مراجعة 8";void Replace(string label,Func<Task> action){var old=tools.Children.OfType<Button>().Single(b=>b.Content?.ToString()==label);int at=tools.Children.IndexOf(old);tools.Children.Remove(old);busyButtons.Remove(old);var b=LocalButton(tools,label,action);tools.Children.Remove(b);tools.Children.Insert(at,b);busyButtons.Add(b);}
  Replace("إضافة ملفات",()=>{var d=new OpenFileDialog{Title="إضافة مستندات المكتبة",Filter="المستندات المدعومة|*.docx;*.doc;*.txt;*.md;*.markdown|Word|*.docx;*.doc|النصوص|*.txt|Markdown|*.md;*.markdown",Multiselect=true};if(d.ShowDialog(this)==true)Merge(d.FileNames.Where(TextFormats.Supported));return Task.CompletedTask;});Replace("إضافة مجلد",async()=>{var d=new OpenFolderDialog{Title="اختر مجلد المستندات المدعومة وفروعه"};if(d.ShowDialog(this)!=true)return;await Work(async token=>{var scan=await Task.Run(()=>Core.Scan(d.FolderName,token),token);token.ThrowIfCancellationRequested();errors.AddRange(scan.Errors);Merge(scan.Files);});});
  if(library.Parent is not DockPanel host||host.Children.OfType<StackPanel>().FirstOrDefault() is not StackPanel panel||panel.Children.OfType<WrapPanel>().FirstOrDefault() is not WrapPanel actions)throw new InvalidOperationException("Library selection toolbar missing");LocalButton(actions,"تحديد الكل",()=>{selected.Clear();selected.UnionWith(files);RefreshLibraryChecks();return Task.CompletedTask;});var clear=LocalButton(actions,"إلغاء تحديد الكل",()=>{selected.Clear();RefreshLibraryChecks();return Task.CompletedTask;});clear.ToolTip="إزالة جميع العلامات بما فيها المخفية بالتصفية؛ عند عدم وجود تحديد مخصص يكون البحث في المكتبة كلها";
 }
}
