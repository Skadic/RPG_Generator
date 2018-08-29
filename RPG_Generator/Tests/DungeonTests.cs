using NUnit.Framework;
using RPG_Generator.World;

namespace RPG_Generator.Tests {
	
	[TestFixture]
	public class DungeonTests {

		[Test]
		public static void TestDungeonPrint() {
			var dungeon = new Dungeon(6, 4);
			dungeon.PrintDungeon();
		}
	}
}