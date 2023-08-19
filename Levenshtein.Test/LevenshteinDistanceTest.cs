namespace Levenshtein.Test
{
    public class LevenshteinDistanceTest
    {
        [Theory]
        [InlineData("abc", "abc", 1, 0)]
        [InlineData("abc", "abd", 1, 1)]
        [InlineData("abc", "abd", 2, 2)]
        [InlineData("a", "a", 1, 0)]
        [InlineData("a", "", 1, 1)]
        public void Walk_Test(string a, string b, int replaceCost, int expected)
        {
            var ld = new LevenshteinDistance(replaceCost);
            var actual = ld.Walk(a, b);
            Assert.Equal(expected, actual);
        }
    }
}
