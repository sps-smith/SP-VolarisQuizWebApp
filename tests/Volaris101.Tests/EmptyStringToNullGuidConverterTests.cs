using System.Text.Json;
using Volaris101.Application.Dtos;
using Volaris101.Web.Infrastructure;

namespace Volaris101.Tests;

public class EmptyStringToNullGuidConverterTests
{
    [Fact]
    public void DeserializeQuestion_WithEmptyQuizIdString_TreatsItAsNull()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new EmptyStringToNullGuidConverter());

        var json = """
            {
              "questionId": "2e61b5d7-6eb2-4f6d-9ef6-b50e1b2d6b1f",
              "displayOrder": 1,
              "questionText": "What is new?",
              "isMultiSelect": false,
              "isActive": true,
              "quizId": "",
              "options": []
            }
            """;

        var dto = JsonSerializer.Deserialize<CmsQuestionDto>(json, options);

        Assert.NotNull(dto);
        Assert.Null(dto!.QuizId);
        Assert.Equal("What is new?", dto.QuestionText);
    }

    [Fact]
    public void DeserializeQuestion_WithValidQuizIdString_PreservesTheGuid()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new EmptyStringToNullGuidConverter());
        var quizId = Guid.NewGuid();

        var json = $$"""
            {
              "questionId": "2e61b5d7-6eb2-4f6d-9ef6-b50e1b2d6b1f",
              "displayOrder": 1,
              "questionText": "What is new?",
              "isMultiSelect": false,
              "isActive": true,
              "quizId": "{{quizId}}",
              "options": []
            }
            """;

        var dto = JsonSerializer.Deserialize<CmsQuestionDto>(json, options);

        Assert.NotNull(dto);
        Assert.Equal(quizId, dto!.QuizId);
    }
}
