# Prompt Template Examples

This document provides examples of different prompt templates you can use with the email assistant.

## Default Template

```json
{
  "AISettings": {
    "PromptTemplate": "You are to rate this email based on the following criteria:\n\n{PromptRatings}\n\nAll other topics should be rated somewhere between 1 and 7 based on how important you think they are\n\nKeep your output in json format, do not provide any other information or tell me why you rated it that way, just provide the rating.\n\nHere is an example:\n{\n  \"Subject\" : \"Bills are due\",\n  \"From\" : \"John\",\n  \"Date\" : \"2021-09-01\",\n  \"Rating\" : 10\n}\n\ninput:\n{EmailJson}"
  }
}
```

## Simplified Template

```json
{
  "AISettings": {
    "PromptTemplate": "Rate this email from 1-10 based on importance:\n\nCriteria:\n{PromptRatings}\n\nEmail: {EmailJson}\n\nReturn only JSON: {\"rating\": number}"
  }
}
```

## Detailed Analysis Template

```json
{
  "AISettings": {
    "PromptTemplate": "Analyze this email and provide a detailed rating:\n\nImportance Criteria:\n{PromptRatings}\n\nEmail to analyze:\n{EmailJson}\n\nPlease respond with JSON containing rating (1-10) and brief reason:\n{\"rating\": number, \"reason\": \"brief explanation\"}"
  }
}
```

## Business-Focused Template

```json
{
  "AISettings": {
    "PromptTemplate": "As a business email filter, evaluate this email's priority:\n\nPriority Guidelines:\n{PromptRatings}\n\nEmail Content:\n{EmailJson}\n\nProvide rating (1-10) in JSON format:\n{\"priority\": number, \"category\": \"urgent|normal|low\"}"
  }
}
```

## Multi-Language Template (Example for Spanish)

```json
{
  "AISettings": {
    "PromptTemplate": "Califica este correo electrónico del 1 al 10 según su importancia:\n\nCriterios:\n{PromptRatings}\n\nCorreo:\n{EmailJson}\n\nResponde solo en formato JSON: {\"calificacion\": numero}"
  }
}
```

## Template Placeholders

- `{PromptRatings}` - Your custom rating criteria from the PromptRatings setting
- `{EmailJson}` - The email content in JSON format (Subject, From, Date, Message)

## Tips for Custom Templates

1. **Keep the JSON format requirement** - The application expects a JSON response with a numeric rating
2. **Be specific about the rating scale** - Clearly specify 1-10 or your preferred scale
3. **Include examples** - Show the AI exactly what format you want
4. **Test with different email types** - Ensure your template works with various email content
5. **Use clear instructions** - Be explicit about what constitutes different rating levels

## Testing Your Template

After changing the `PromptTemplate`, restart the application and test with a few emails to ensure the AI responds in the expected format.
