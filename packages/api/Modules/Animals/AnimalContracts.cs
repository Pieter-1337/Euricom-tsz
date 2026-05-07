using System.ComponentModel.DataAnnotations;

namespace Api.Modules.Animals;

public class CreateAnimalRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Species { get; set; } = string.Empty;

    [Range(0, 200)]
    public int Age { get; set; }
}

public class UpdateAnimalRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Species { get; set; } = string.Empty;

    [Range(0, 200)]
    public int Age { get; set; }
}
