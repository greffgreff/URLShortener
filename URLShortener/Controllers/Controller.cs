using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace URLShortener.Controllers;

[ApiController]
public class Controller : ControllerBase
{
    private static readonly Random random = new();
    private readonly IMemoryCache cache;
    
    public Controller(IMemoryCache cache)
    {
        this.cache = cache;
    }

    [HttpPost("shorten")]
    public IActionResult ShortenUrl([FromForm] char alias, [FromForm] string url)
    {
        if (!char.IsLetter(alias))
        {
            return BadRequest("`alias` can only be a single letter.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("`url` cannot be null or empty.");
        }

        url = Uri.UnescapeDataString(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var validUri))
        {
            return BadRequest("`url` is not a valid URL.");
        }

        var key = ToRandomString(alias + validUri.ToString());
        var host = HttpContext.Request.Host;
        var scheme = HttpContext.Request.Scheme;
        var redirect = $"{scheme}://{host}/{alias}/{key}";

        var expiration = TimeSpan.FromMinutes(60);

        cache.Set(key, validUri.ToString(), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
        });

        var payload = new
        {
            ShortUrl = redirect,
            Expiration = DateTime.Now + expiration,
            Lifespan = expiration,
        };

        return Ok(payload);
    }

    public static string ToRandomString(string input, int length = 10)
    {
        using var sha1 = SHA1.Create();
        byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        string hexHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        int startIndex = random.Next(0, hexHash.Length - length);

        string letterMapping = "abcdefghijklmnopqrstuvwxyz";
        char[] randomLetters = hexHash
            .Substring(startIndex, length)
            .Select(hexChar =>
            {
                char letter = letterMapping[Convert.ToInt32(hexChar.ToString(), 16)];
                return random.Next(2) == 0 ? char.ToLower(letter) : char.ToUpper(letter);
            })
            .ToArray();

        return new string(randomLetters);
    }

    [HttpGet("{alias}/{key}")]
    public IActionResult UrlToRedirect(string key)
    {
        var url = cache.Get<string>(key);
        
        if (string.IsNullOrWhiteSpace(url))
        {
            return NotFound("Redirect URL not found or already expired.");
        }

        return Redirect(url);
    }
}
