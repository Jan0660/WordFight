using Microsoft.AspNetCore.Mvc;

namespace WordFight.Controllers;

[Route("[controller]")]
public class TestingController : Controller
{
    public TestingController()
    {
    }
    [HttpGet("feelings", Name = "GetStatus")]
    
    public JsonResult Feelings()
    {
        return Json(new Status(Globals.Data.Words));
    }

    public record Status(Word[] Words);
}