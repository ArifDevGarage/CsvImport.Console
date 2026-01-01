using System;
using System.Collections.Generic;

namespace CsvImport.ConsoleApp;

public partial class ExtEmployeeExternal
{
    public int Id { get; set; }

    public string EmployeeId { get; set; } = null!;

    public string EmployeeName { get; set; } = null!;

    public string PositionId { get; set; } = null!;

    public string PositionName { get; set; } = null!;

    public string? Area { get; set; }

    public string? Directorate { get; set; }

    public string? Function { get; set; }

    public string Department { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Level { get; set; }

    public string SuperiorId { get; set; } = null!;

    public string SuperiorPositionId { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public DateTime LastUpdate { get; set; }
}
