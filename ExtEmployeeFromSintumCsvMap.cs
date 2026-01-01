using CsvHelper.Configuration;

namespace CsvImport.ConsoleApp;

public sealed class ExtEmployeeFromSintumCsvMap : ClassMap<ExtEmployeeFromSintumCsvRow>
{
    public ExtEmployeeFromSintumCsvMap()
    {
        Map(m => m.EmployeeId).Name("EmployeeId", "Employee ID", "EmpId");
        Map(m => m.EmployeeName).Name("EmployeeName", "Employee Name", "EmpName");

        Map(m => m.PositionId).Name("PositionId", "Position ID");
        Map(m => m.PositionName).Name("PositionName", "Position Name");

        Map(m => m.Area).Name("Area");
        Map(m => m.PlantArea).Name("PlantArea", "Plant Area");
        Map(m => m.Directorate).Name("Directorate");
        Map(m => m.Function).Name("Function");
        Map(m => m.Department).Name("Department");

        Map(m => m.Email).Name("Email");
        Map(m => m.Level).Name("Level");

        Map(m => m.SuperiorId).Name("SuperiorId", "Superior ID");
        Map(m => m.SuperiorPositionId).Name("SuperiorPositionId", "Superior Position ID");

        Map(m => m.UserName).Name("UserName", "Username");
        Map(m => m.Unit).Name("Unit");
        Map(m => m.Posgrd).Name("Posgrd", "PosGrd");

        Map(m => m.CostCenter).Name("CostCenter", "Cost Center");
        Map(m => m.Entity).Name("Entity");

        Map(m => m.LastUpdate).Name("LastUpdate", "Last Update", "UpdatedAt");
        Map(m => m.HelperIsDelegate).Name("HelperIsDelegate", "Helper_IsDelegate");
        Map(m => m.HelperEmployeePositionTypeId).Name("HelperEmployeePositionTypeId", "Helper_EmployeePositionTypeId");
    }
}
