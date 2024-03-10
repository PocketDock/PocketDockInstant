using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PocketDockUI.Models;

public class LaunchParameters
{
    [Required]
    [FromForm(Name = "g-recaptcha-response")]
    public string RecaptchaToken { get; set; }

    public string SelectedVersion { get; set; }
    public string SelectedRegion { get; set; }
}