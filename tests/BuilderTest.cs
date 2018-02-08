using System.Text;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GolombCodeFilterSet;

namespace GolombCodedFilterSet.UnitTests
{
	[TestClass]
	public class BuilderTest
	{
		[TestMethod]
		public void BuildFilterAndMatchValues()
		{
			var names = from name in new[] { "New York", "Amsterdam", "Paris", "Buenos Aires", "La Habana" }
						select Encoding.ASCII.GetBytes(name);

			var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
			var filter = Filter.Build(key, 0x10, names);

			// The filter should match all ther values that were added.
			foreach(var name in names)
			{
				Assert.IsTrue(filter.Match(name, key));
			}

			// The filter should NOT match any extra value.
			Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Porto Alegre"), key));
			Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Madrid"), key));

			// The filter should match because it has one element indexed: Buenos Aires.
			var otherCities = new[] { "La Paz", "Barcelona", "El Cairo", "Buenos Aires", "Asunción" };
			var otherNames = from name in otherCities select Encoding.ASCII.GetBytes(name);
			Assert.IsTrue(filter.MatchAny(otherNames, key));

			// The filter should NOT match because it doesn't have any element indexed.
			var otherCities2 = new[] { "La Paz", "Barcelona", "El Cairo", "Córdoba", "Asunción" };
			var otherNames2 = from name in otherCities2 select Encoding.ASCII.GetBytes(name);
			Assert.IsFalse(filter.MatchAny(otherNames2, key));
		}
	}
}
