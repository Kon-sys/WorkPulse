using ClosedXML.Excel;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using WorkPulse.Infrastructure;
using System.Globalization;

namespace WorkPulse.Api;
public static class ReportExport
{
    private sealed class ReportFontResolver(string path) : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName,bool isBold,bool isItalic)=>new("report",isBold,isItalic);
        public byte[] GetFont(string faceName)=>File.ReadAllBytes(path);
    }
    public static void ConfigureFont()
    {
        var path=Environment.GetEnvironmentVariable("WORKPULSE_REPORT_FONT");
        if(string.IsNullOrEmpty(path) && OperatingSystem.IsWindows())path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");
        if(path is not null && File.Exists(path))GlobalFontSettings.FontResolver=new ReportFontResolver(path);
    }
    public static byte[] Xlsx(ReportResult report)
    {
        using var book=new XLWorkbook();var sheet=book.AddWorksheet("Сотрудники");
        string[] headers=["Сотрудник","Подразделение","Продуктивно, с","Нецелевое, с","Нейтрально, с","Простой, с","Unknown, с","Обед и отсутствие, с","Продуктивность, %","Покрытие, %","Нарушения","Соблюдение графика, %"];
        for(int i=0;i<headers.Length;i++)sheet.Cell(1,i+1).Value=headers[i];
        for(int n=0;n<report.Employees.Count;n++)
        {
            var e=report.Employees[n];var m=e.Metrics;var row=n+2;
            sheet.Cell(row,1).Value=e.Name;sheet.Cell(row,2).Value=e.DepartmentName;
            double[] values=[m.ProductiveSeconds,m.NonTargetSeconds,m.NeutralSeconds,m.IdleSeconds,m.UnknownSeconds,m.ExcludedSeconds];
            for(int i=0;i<values.Length;i++)sheet.Cell(row,i+3).Value=values[i];
            if(m.ProductiveShare is { } share)sheet.Cell(row,9).Value=share;
            if(m.Coverage is { } coverage)sheet.Cell(row,10).Value=coverage;
            sheet.Cell(row,11).Value=e.Violations;
            if(e.ScheduleCompliance is { } compliance)sheet.Cell(row,12).Value=compliance;
        }
        sheet.Row(1).Style.Font.Bold=true;sheet.SheetView.FreezeRows(1);sheet.Columns().Width=22;sheet.Column(1).Width=32;sheet.Row(1).Style.Alignment.WrapText=true;
        var departments=book.AddWorksheet("Подразделения");
        string[] departmentHeaders=["Подразделение","Продуктивность, %","Нецелевое, %","Простой, %","Покрытие, %","Нарушения","Соблюдение графика, %"];
        for(int i=0;i<departmentHeaders.Length;i++)departments.Cell(1,i+1).Value=departmentHeaders[i];
        for(int n=0;n<report.Departments.Count;n++)
        {
            var d=report.Departments[n];departments.Cell(n+2,1).Value=d.Name;
            double?[] values=[d.Metrics.ProductiveShare,d.Metrics.NonTargetShare,d.Metrics.IdleShare,d.Metrics.Coverage,d.Violations,d.ScheduleCompliance];
            for(int i=0;i<values.Length;i++)if(values[i] is { } value)departments.Cell(n+2,i+2).Value=value;
        }
        departments.Row(1).Style.Font.Bold=true;departments.Row(1).Style.Alignment.WrapText=true;departments.Columns().Width=24;departments.Column(1).Width=32;departments.SheetView.FreezeRows(1);
        var info=book.AddWorksheet("Методика");info.Cell(1,1).Value="WorkPulse";info.Cell(2,1).Value=report.Start.ToString("O");info.Cell(3,1).Value=report.End.ToString("O");info.Cell(4,1).Value=report.Basis;
        info.Cell(5,1).Value="Доли: категория / известное время без обеда и разрешённого отсутствия. Unknown исключён из долей и снижает покрытие. Пустая ячейка – нет знаменателя.";
        info.Column(1).Width=110;info.Rows().Style.Alignment.WrapText=true;
        using var output=new MemoryStream();book.SaveAs(output);return output.ToArray();
    }
    public static byte[] Pdf(ReportResult report)
    {
        using var document=new PdfDocument();document.Info.Title="WorkPulse – отчёт по рабочему времени";
        var font=new XFont("WorkPulseReport",10);XGraphics? gfx=null;double y=0;int pageNumber=0;
        void Line(string value)
        {
            // Wrap even long unbroken identifiers; never draw beyond the page width.
            var pieces=new List<string>();var current="";
            foreach(var ch in value.Replace('\r',' ').Replace('\n',' ').Replace('\t',' '))
            {
                if(gfx is null || gfx.MeasureString(current+ch,font).Width<500){current+=ch;continue;}
                pieces.Add(current);current=ch.ToString();
            }
            pieces.Add(current);
            foreach(var text in pieces)
            {
                if(gfx is null || y>770)
                {
                    gfx?.Dispose();var page=document.AddPage();gfx=XGraphics.FromPdfPage(page);y=42;pageNumber++;
                    gfx.DrawString($"WorkPulse | {pageNumber}",font,XBrushes.DarkSlateGray,new XPoint(42,y));y+=25;
                }
                gfx.DrawString(text,font,XBrushes.Black,new XPoint(42,y));y+=16;
            }
        }
        Line("Отчёт по рабочему времени");
        Line($"Период UTC: {report.Start:yyyy-MM-dd HH:mm} – {report.End:yyyy-MM-dd HH:mm}");
        Line(report.Basis);Line("Доли рассчитаны по известному времени без обеда и разрешённого отсутствия.");
        string Percent(double? value)=>value is { } number ? number.ToString("F1",CultureInfo.InvariantCulture)+"%" : "Нет данных";
        foreach(var e in report.Employees)
        {
            Line("");Line(e.Name+" | "+e.DepartmentName);
            Line($"Продуктивно {e.Metrics.ProductiveSeconds:F3} с; нецелевое {e.Metrics.NonTargetSeconds:F3} с; простой {e.Metrics.IdleSeconds:F3} с; Unknown {e.Metrics.UnknownSeconds:F3} с.");
            Line($"Нейтрально {e.Metrics.NeutralSeconds:F3} с; обед и разрешённое отсутствие {e.Metrics.ExcludedSeconds:F3} с.");
            Line($"Продуктивность {Percent(e.Metrics.ProductiveShare)}; покрытие {Percent(e.Metrics.Coverage)}; нарушения {e.Violations}; соблюдение графика {Percent(e.ScheduleCompliance)}.");
        }
        gfx?.Dispose();using var output=new MemoryStream();document.Save(output,false);return output.ToArray();
    }
}
