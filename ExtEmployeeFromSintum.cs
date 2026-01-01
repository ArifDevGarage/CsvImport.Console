using System;
using System.Collections.Generic;

namespace CsvImport.ConsoleApp;

public partial class ExtEmployeeFromSintum
{
    public int Id { get; set; }

    public string? EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public string PositionId { get; set; } = null!;

    public string? PositionName { get; set; }

    public string? Area { get; set; }

    public string? PlantArea { get; set; }

    public string? Directorate { get; set; }

    public string? Function { get; set; }

    public string? Department { get; set; }

    public string? Email { get; set; }

    public string? Level { get; set; }

    public string? SuperiorId { get; set; }

    public string? SuperiorPositionId { get; set; }

    public string? UserName { get; set; }

    public string? Unit { get; set; }

    public string? Posgrd { get; set; }

    public string? CostCenter { get; set; }

    public string? Entity { get; set; }

    public DateTime? LastUpdate { get; set; }

    public bool? HelperIsDelegate { get; set; }

    public byte? HelperEmployeePositionTypeId { get; set; }
}
