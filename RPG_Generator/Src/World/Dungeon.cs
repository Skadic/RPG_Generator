using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Windows.Media;

namespace RPG_Generator.World {
	
	
	public class Dungeon {
		public int Width { get; }
		public int Height { get; }

		private readonly Tile[,] tiles;
		private readonly (int, int) entrancePos;
		private Dictionary<Tile, int> distances;
		
		
		public Dungeon(int size) : this(size, size) {}

		public Dungeon(int width, int height) {
			Width = width;
			Height = height;
			tiles = new Tile[width, height];
			var rand = new Random(DateTime.Now.Millisecond);
			entrancePos = (rand.Next(Width), rand.Next(Height));
			BuildDungeon();
			CalcDistance();
			Populate();
		}

		private void BuildDungeon() {
			var rand = new Random(DateTime.Now.Millisecond);
			var startX = entrancePos.Item1;
			var startY = entrancePos.Item2;
			
			//number of tiles in the dungeon
			var numTiles = Width * Height;
				
			//Current number of tiles with min. 1 open side
			var builtTiles = 0;

			var built = new bool[Width, Height];
				
			//Minimum desired percentage amount of tiles with open sides
			const double minBuiltTiles = 0.7;

			Console.WriteLine(@"Start coords: [{0},{1}]", startX, startY);
			
			while((float) builtTiles / numTiles < minBuiltTiles){
				builtTiles = 0;
				for(var y = 0; y < Height; y++){
					for(var x = 0; x < Width; x++){
						tiles[x, y] = new Tile(x, y, this);
						built[x, y] = false;
					}
				}
				
				tiles[startX, startY].Type = Tile.TileType.Entrance;

				Build(startX, startY);
			}

			void OpenRandomSide(int x, int y) {
				
				//Random order of sides to check
				var numbers = new[] {0, 1, 2, 3};
				numbers = numbers.OrderBy(a => rand.Next()).ToArray();
				
				//Chance to connect to already built tiles
				const int connectChance = 20;
				
				for(var i = 0; i < 4; i++){
					var tile = tiles[x, y];
					var openSides = tile.OpenSidesCount();
					
					//If 4 sides are open, no more sides can be opened
					if (openSides == 4) return;

					//The side to open
					var side = Sides.SideByNumber(numbers[i]);
					
					//If the side is already open, try another side
					if(tile.SideOpen(side)) continue;
					
					//The tile to be connected to and whether or not that tile is built already
					Tile toConnect;
					bool tileBuilt;
					
					switch(side){
						case Side.Top:
							if(y == 0) continue;
							else tile.Up = true;
							
							tileBuilt = built[x, y - 1];
							toConnect = tiles[x, y - 1];
							break;
						
						case Side.Left:
							if(x == 0) continue;
							else tile.Left = true;
							
							tileBuilt = built[x - 1, y];
							toConnect = tiles[x - 1, y];
							break;
						
						case Side.Bottom:
							if(y == Height - 1) continue;
							else tile.Down = true;
							
							tileBuilt = built[x, y + 1];
							toConnect = tiles[x, y + 1];
							break;
						
						case Side.Right:
							if(x == Width - 1) continue;
							else tile.Right = true;
							
							tileBuilt = built[x + 1, y];
							toConnect = tiles[x + 1, y];
							break;
						
						default:
							throw new ArgumentException();
					}
					
					//If the tile to be connected is already built, connect at a certain chance anyway
					//This is so that the Dungeon doesn't have insanely long paths to get from one place to another 
					if(!PercentCheck(connectChance) && tileBuilt && !toConnect.SideOpen(side.Opposite())){
						tile.SetSide(side, false);
						continue;
					}
					
					//If the tile to be connected already has a side open and isn't built yet, only connect at a very small chance
					//Otherwise the dungeons has many small loops
					if(!tileBuilt && toConnect.OpenSidesCount() > 0 && PercentCheck(95)){
						tile.SetSide(side, false);
						continue;
					}
					
					//Open the corresponding side of the tile to be connected to
					toConnect.SetSide(side.Opposite(), true);
					
					//A side has been successfully opened and the loop can be exited
					break;
				}
			}

			void Build(int x, int y) {
				if(!(0 <= x && x < Width && 0 <= y && y < Height) || built[x, y]) return;
				for(var i = 0; i < rand.Next((float) builtTiles / numTiles >= minBuiltTiles ? 0 : 1, 3); i++){
					OpenRandomSide(x, y);
				}
				
				builtTiles++;
				built[x, y] = true;
				
				ForOpenAndUnbuilt(x, y, Build);
			}

			void BuildBreadthFirst(int xStart, int yStart) {
				void BuildInternal(int x, int y) {
					if(!(0 <= x && x < Width && 0 <= y && y < Height) || built[x, y]) return;
					for(var i = 0; i < rand.Next((float) builtTiles / numTiles >= minBuiltTiles ? 0 : 1, 3); i++){
						OpenRandomSide(x, y);
					}
				}
				
				var queue = new Queue<ValueTuple<int, int>>();
				queue.Enqueue((xStart, yStart));

				while(queue.Count > 0){
					var (x, y) = queue.Dequeue();
					BuildInternal(x, y);
					ForOpenAndUnbuilt(x, y, (x2, y2) => {
						queue.Enqueue((x2, y2));
					});
					
					builtTiles++;
					built[x, y] = true;
				}
			}
			
			void ForOpenAndUnbuilt(int x, int y, Action<int, int> action) {
				var tile = tiles[x, y];
				if(tile.Up && !built[x, y - 1]){
					action.Invoke(x, y - 1);
				}
				if(tile.Left && !built[x - 1, y]){
					action.Invoke(x - 1, y);
				}
				if(tile.Down && !built[x, y + 1]){
					action.Invoke(x, y + 1);
				}
				if(tile.Right && !built[x + 1, y]){
					action.Invoke(x + 1, y);
				}
			}

			bool PercentCheck(int percent) {
				return rand.Next(1, 101) <= percent;
			}
		}

		private void CalcDistance() {
			var startX = entrancePos.Item1;
			var startY = entrancePos.Item2;

			var (adjacencyList, traversed, distance) = Init();
			var queue = new Queue<Tile>();
			queue.Enqueue(tiles[startX, startY]);
			distance[tiles[startX, startY]] = 0;
			
			while(queue.Count > 0){
				var u = queue.Dequeue();
				foreach(var v in adjacencyList[u]){
					if(traversed[v]) continue;
					traversed[v] = true;
					distance[v] = distance[u] + 1;
					queue.Enqueue(v);
				}
			}
			
			distances = distance;

			(Dictionary<Tile, List<Tile>>, Dictionary<Tile, bool>, Dictionary<Tile, int>) Init() {
				var lists = new Dictionary<Tile, List<Tile>>();
				var tileTraversed = new Dictionary<Tile, bool>();
				var dist = new Dictionary<Tile, int>();
				
				foreach(var tile in tiles){
					lists[tile] = new List<Tile>();
					tileTraversed[tile] = false;
					dist[tile] = int.MaxValue;
					foreach(var side in (Side[]) Enum.GetValues(typeof(Side))){
						if(tile.SideOpen(side))
							lists[tile].Add(tile.TileOnSide(side));
					}
				}

				return (lists, tileTraversed, dist);
			}
		}

		private void Populate() {
			var maxDistTile = distances.First(x => x.Value == distances.Values.Max()).Key;
			maxDistTile.Type = Tile.TileType.Boss;
		}

		public void PrintDungeon() {
			var row1 = new StringBuilder();
			var row2 = new StringBuilder();
			var row3 = new StringBuilder();
			
			for(var y = 0; y < Height; y++){
				
				for(var x = 0; x < Width; x++){
					AddTileToStringBuilder(tiles[x, y]);
				}

				Console.WriteLine(row1);
				Console.WriteLine(row2);
				Console.WriteLine(row3);
				
				row1.Clear();
				row2.Clear();
				row3.Clear();
			}

			Console.WriteLine();

			void AddTileToStringBuilder(Tile tile) {
				var special = " ";
				const string wall = "█";

				switch(tile.Type){
					case Tile.TileType.Default:
						special = " ";
						break;
					default:
						special = tile.Type.ToString().Substring(0, 1);
						break;
				}

				if(tile.OpenSidesCount() > 0){ 

					row1.Append(wall);
					row2.Append(!tile.Left ? wall : " ");
					row3.Append(wall);

					row1.Append(!tile.Up ? wall : " ");
					row2.Append(special);
					row3.Append(!tile.Down ? wall : " ");

					row1.Append(wall);
					row2.Append(!tile.Right ? wall : " ");
					row3.Append(wall);
				}
				else{
					row1.Append("   ");
					row2.Append("   ");
					row3.Append("   ");
				}
			}
		}
		
		private class Tile {
			public enum TileType {
				Entrance, Trap, Default, Fight, Boss
			}

			public Tile(int x, int y, Dungeon map) {
				Type = TileType.Default;
				X = x;
				Y = y;
				Map = map;
			}

			public int X { get; }
			public int Y { get; }
			
			private Dungeon Map { get; }

			public bool Up { get; set; }

			public bool Down { get; set; }

			public bool Left { get; set; }

			public bool Right { get; set; }
			
			public TileType Type { get; set; }
			
			public Tile TileOnSide(Side side) {
				switch(side){
					case Side.Top:
						if(Y != 0)
							return Map.tiles[X, Y - 1];
						break;
					case Side.Left:
						if(X != 0)
							return Map.tiles[X - 1, Y];
						break;
					case Side.Bottom:
						if(Y != Map.Height - 1)
							return Map.tiles[X, Y + 1];
						break;
					case Side.Right:
						if(X != Map.Width - 1)
							return Map.tiles[X + 1, Y];
						break;
				}
				return null;
			}

			public bool SideOpen(Side side) {
				switch(side){
					case Side.Top:
						return Up;
					case Side.Left:
						return Left;
					case Side.Bottom:
						return Down;
					case Side.Right:
						return Right;
				}

				return false;
			}

			public void SetSide(Side side, bool value) {
				switch(side){
					case Side.Top:
						Up = value;
						break;
					case Side.Left:
						Left = value;
						break;
					case Side.Bottom:
						Down = value;
						break;
					case Side.Right:
						Right = value;
						break;
				}
			}

			public int OpenSidesCount() {
				return (Up ? 1 : 0) + (Down ? 1 : 0) + (Left ? 1 : 0) + (Right ? 1 : 0);
			}
		}
	}

	public enum Side {
		Top, Bottom, Left, Right
	}
	
	public static class Sides {
		
		public static Side Opposite(this Side side) {
			switch(side){
				case Side.Top:
					return Side.Bottom;
				case Side.Bottom:
					return Side.Top;
				case Side.Left:
					return Side.Right;
				case Side.Right:
					return Side.Left;
			}

			throw new ArgumentException("Invalid Side");
		}
		
		public static Side SideByNumber(int n) {
			switch(n){
				case 0:
					return Side.Top;
				case 1:
					return Side.Left;
				case 2:
					return Side.Bottom;
				case 3:
					return Side.Right;
				default:
					throw new ArgumentException();
			}
		}
	}
}