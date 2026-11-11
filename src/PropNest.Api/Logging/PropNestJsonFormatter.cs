using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;

namespace PropNest.Api.Logging;

/// <summary>
/// Custom JSON formatter producing single-line JSON log events matching Task 203 requirements:
/// - Top-level properties: Timestamp (ISO 8601), Level, Message, CorrelationId.
/// - Top-level MachineName, ThreadId, WorkflowId, and any other enriched properties.
/// - Enables searching all logs for a request via grep CorrelationId.
/// </summary>
public sealed class PropNestJsonFormatter : ITextFormatter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false
    };

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();

            // Core required attributes
            writer.WriteString("Timestamp", logEvent.Timestamp.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            writer.WriteString("Level", logEvent.Level.ToString());
            writer.WriteString("Message", logEvent.RenderMessage(CultureInfo.InvariantCulture));

            // If CorrelationId is present, ensure it is written at top level
            if (logEvent.Properties.TryGetValue("CorrelationId", out var correlationProp))
            {
                writer.WritePropertyName("CorrelationId");
                WritePropertyValue(writer, correlationProp);
            }

            // Write all other enriched properties
            foreach (var (key, value) in logEvent.Properties)
            {
                if (key == "CorrelationId")
                {
                    continue; // Already emitted
                }

                writer.WritePropertyName(key);
                WritePropertyValue(writer, value);
            }

            if (logEvent.Exception != null)
            {
                writer.WriteString("Exception", logEvent.Exception.ToString());
            }

            writer.WriteEndObject();
        }

        output.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static void WritePropertyValue(Utf8JsonWriter writer, LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue scalar:
                switch (scalar.Value)
                {
                    case null:
                        writer.WriteNullValue();
                        break;
                    case string s:
                        writer.WriteStringValue(s);
                        break;
                    case bool b:
                        writer.WriteBooleanValue(b);
                        break;
                    case int i:
                        writer.WriteNumberValue(i);
                        break;
                    case long l:
                        writer.WriteNumberValue(l);
                        break;
                    case double d:
                        writer.WriteNumberValue(d);
                        break;
                    case decimal m:
                        writer.WriteNumberValue(m);
                        break;
                    case float f:
                        writer.WriteNumberValue(f);
                        break;
                    default:
                        writer.WriteStringValue(Convert.ToString(scalar.Value, CultureInfo.InvariantCulture));
                        break;
                }
                break;

            case SequenceValue seq:
                writer.WriteStartArray();
                foreach (var item in seq.Elements)
                {
                    WritePropertyValue(writer, item);
                }
                writer.WriteEndArray();
                break;

            case StructureValue structure:
                writer.WriteStartObject();
                foreach (var prop in structure.Properties)
                {
                    writer.WritePropertyName(prop.Name);
                    WritePropertyValue(writer, prop.Value);
                }
                writer.WriteEndObject();
                break;

            case DictionaryValue dict:
                writer.WriteStartObject();
                foreach (var kvp in dict.Elements)
                {
                    writer.WritePropertyName(kvp.Key.Value?.ToString() ?? "null");
                    WritePropertyValue(writer, kvp.Value);
                }
                writer.WriteEndObject();
                break;

            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
