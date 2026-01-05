using System.Text;

namespace Adliance.Kimai;

public class HtmlWriter
{
    private readonly StringBuilder _sb = new();

    public HtmlWriter(string title, string subTitle)
    {
        W($$"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{title}}</title>
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@picocss/pico/css/pico.min.css">
                <style>body { font-size: 66%; }</style>
              </head>
              <body>
            """);

        W($"""
           <header class="container">
           <hgroup>
             <h1>{title}</h1>
             <p>{subTitle}</p>
           </hgroup>
           </header>
           <main class="container">
           """);
    }

    public void W(string line)
    {
        _sb.AppendLine(line);
    }

    public override string ToString()
    {
        W("""
          </main>
          </body>
          </html>
          """);

        return _sb.ToString();
    }
}
