#r "nuget: Scriban, 7.0.6"
using Scriban;

var templates = new[] {
    "{{ name",
    "{{ if true }}yes",
    "{{ for x in items }}item",
    "{{ if }}",
    "{{ if true }}{{ for x in y }}{{ end }}",
    "{{ \"unclosed }}",
    "{{ 1 + }}",
    "{{ if a }}{{ if b }}{{ end }}",
    "{{ | }}",
    "{{ func test }}body"
};

foreach (var content in templates)
{
    var t = Template.Parse(content, "test.scriban");
    Console.WriteLine($"HasErrors={t.HasErrors}: \"{content}\"");
    if (t.HasErrors)
        foreach (var m in t.Messages)
            Console.WriteLine($"  -> {m.Message}");
}
