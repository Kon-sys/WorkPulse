using Xunit;
using WorkPulse.Api;
using WorkPulse.Application;
using WorkPulse.Infrastructure;
using ClosedXML.Excel;
namespace WorkPulse.Tests;
public sealed class ApiValidationTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public void BlankTextRejected(string? value)=>Assert.Equal(400,Assert.Throws<RequestFault>(()=>InputValidation.Text(value)).Status);
    [Fact]
    public void TextTrimmedAndMaximumEnforced(){Assert.Equal("abc",InputValidation.Text(" abc ",3));Assert.Throws<RequestFault>(()=>InputValidation.Text("abcd",3));}
    [Theory]
    [InlineData(0,false)] [InlineData(11,false)] [InlineData(12,true)] [InlineData(128,true)] [InlineData(129,false)]
    public void PasswordBounds(int length,bool valid){if(valid)InputValidation.Password(new string('a',length));else Assert.Throws<RequestFault>(()=>InputValidation.Password(new string('a',length)));}
    [Fact]
    public void NullPasswordRejected()=>Assert.Throws<RequestFault>(()=>InputValidation.Password(null));
    [Fact]
    public void AdministrationRequiresOrganizationAdmin()
    {
        var user=new UserAccess(Guid.NewGuid(),Guid.NewGuid(),null,"x",Guid.NewGuid(),[new("hr","organization",null)]);
        Assert.Equal(403,Assert.Throws<RequestFault>(()=>WorkspaceStore.Admin(user)).Status);
        WorkspaceStore.Admin(user with {Grants=[new("admin","organization",null)]});
    }
    [Fact]
    public void XlsxExportKeepsMissingValuesEmptyAndNamesAsText()
    {
        var start=DateTimeOffset.UtcNow.AddHours(-1);var metrics=MetricsCalculator.Calculate([],start,start.AddHours(1));
        var report=new ReportResult(start,start.AddHours(1),[new(Guid.NewGuid(),"=1+1",Guid.NewGuid(),"Отдел",metrics,0,0,0,null,[])],[],"Нет данных");
        var bytes=ReportExport.Xlsx(report);using var book=new XLWorkbook(new MemoryStream(bytes));var sheet=book.Worksheet("Сотрудники");
        Assert.Equal("=1+1",sheet.Cell(2,1).GetString());Assert.Empty(sheet.Cell(2,1).FormulaA1);
        Assert.True(sheet.Cell(2,9).IsEmpty());Assert.True(sheet.Cell(2,10).IsEmpty());
    }
    [Fact]
    public void PdfExportPaginatesUnicodeAndLongNames()
    {
        ReportExport.ConfigureFont();
        var start=DateTimeOffset.UtcNow.AddDays(-1);
        var metrics=MetricsCalculator.Calculate([],start,start.AddHours(8));
        var employees=Enumerable.Range(0,100).Select(i=>new EmployeeReport(Guid.NewGuid(),"Сотрудник "+i+" "+new string('Я',180),Guid.NewGuid(),"Разработка",metrics,0,0,0,null,[])).ToArray();
        var bytes=ReportExport.Pdf(new(start,start.AddHours(8),employees,[],"Тест Unicode и нескольких страниц"));
        Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(bytes,0,5));
        using var pdf=PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(bytes),PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount>1);
        var directory=Environment.GetEnvironmentVariable("WORKPULSE_TEST_ARTIFACTS");
        if(directory is not null){Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"unit-export.pdf"),bytes);}
    }
}
