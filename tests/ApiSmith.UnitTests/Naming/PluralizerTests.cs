using ApiSmith.Naming;

namespace ApiSmith.UnitTests.Naming;

public sealed class PluralizerTests
{
    [Theory]
    [InlineData("Customer", "Customers")]
    [InlineData("Box", "Boxes")]
    [InlineData("Bus", "Buses")]
    [InlineData("Quiz", "Quizzes")]   // -z rule
    [InlineData("Country", "Countries")]
    [InlineData("Day", "Days")]        // y after vowel: just add s
    [InlineData("Knife", "Knives")]
    [InlineData("Wolf", "Wolves")]
    [InlineData("Person", "People")]
    [InlineData("Child", "Children")]
    [InlineData("Index", "Indexes")]
    [InlineData("Status", "Statuses")]
    public void Pluralize_handles_common_cases(string input, string expected)
    {
        Assert.Equal(expected, Pluralizer.Pluralize(input));
    }

    [Theory]
    [InlineData("Customers", "Customer")]
    [InlineData("Boxes", "Box")]
    [InlineData("Countries", "Country")]
    [InlineData("Knives", "Knife")]
    [InlineData("People", "Person")]
    public void Singularize_handles_common_cases(string input, string expected)
    {
        Assert.Equal(expected, Pluralizer.Singularize(input));
    }

    [Theory]
    [InlineData("equipment")]
    [InlineData("information")]
    [InlineData("metadata")]
    public void Uncountables_pass_through(string word)
    {
        Assert.Equal(word, Pluralizer.Pluralize(word));
        Assert.Equal(word, Pluralizer.Singularize(word));
    }
}
