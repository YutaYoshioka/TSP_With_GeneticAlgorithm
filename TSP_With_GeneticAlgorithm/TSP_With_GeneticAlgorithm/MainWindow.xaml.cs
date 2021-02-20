using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

using Pen = System.Drawing.Pen;
using Color = System.Drawing.Color;
using Brushes = System.Drawing.Brushes;

namespace TSP_With_GeneticAlgorithm
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 都市情報
		/// </summary>
		public struct City
		{
			public double x;

			public double y;

			/// <summary>
			/// イメージ上の座標
			/// </summary>
			public int imgX;

			/// <summary>
			/// イメージ上の座標
			/// </summary>
			public int imgY;
		}

		City[] citiesData;

		/// <summary>
		/// 出力するイメージのサイズ
		/// </summary>
		int imageSize = 1000;

		/// <summary>
		/// 集団サイズ
		/// </summary>
		int groupSize;

		/// <summary>
		/// 終了世代数
		/// </summary>
		int generationsNum;

		/// <summary>
		/// 都市数
		/// </summary>
		int urbanNum;


		Random rand = new Random();

		private async void startButton_Click(object sender, RoutedEventArgs e)
		{
			await GA_Start();
		}

		private Task GA_Start()
		{
			// 非同期で実行
			return Task.Run(() =>
			{
				Dispatcher.Invoke((Action)(() =>
				{
					groupSize = int.Parse(groupSize_tb.Text);
					generationsNum = int.Parse(generationsNum_tb.Text);
					urbanNum = int.Parse(urbanNum_tb.Text);
				}));


				citiesData = LoadCityData("city" + urbanNum + ".tsp");


				// ルート情報，染色体．
				// 最後の都市から最初に戻るのは考慮されない．
				var route = new int[citiesData.Length];

				// 0,1,2,3,… に初期化
				for (int i = 0; i < route.Length; i++)
				{
					route[i] = i;
				}

				// 初期集団をランダムに作成

				// 染色体
				var routes = new int[groupSize][];

				// それぞれの適合度
				var routes_Suitability = new double[groupSize];

				// 適合度の合計
				var suitabilitySum = 0.0;

				var minSuitability = 100000.0;
				var maxSuitability = 0.0;

				for (int j = 0; j < groupSize; j++)
				{
					// ランダムに並び替え
					routes[j] = route.OrderBy(i => Guid.NewGuid()).ToArray();
					routes_Suitability[j] = 1 / CalculateDistance(routes[j]);
					// suitabilitySum += routes_Suitability[j];

					if (routes_Suitability[j] < minSuitability)
					{
						minSuitability = routes_Suitability[j];
					}

					if (routes_Suitability[j] > maxSuitability)
					{
						maxSuitability = routes_Suitability[j];
					}
				}

				CalculateSuitability(routes, routes_Suitability, ref suitabilitySum, ref minSuitability, ref maxSuitability);

				for (int i = 0; i < generationsNum; i++)
				{
					// 最善解を退避
					var rTemp = new int[urbanNum];
					routes[0].CopyTo(rTemp, 0);
					var dTemp = CalculateDistance(rTemp);

					CalculateSuitability(routes, routes_Suitability, ref suitabilitySum, ref minSuitability, ref maxSuitability);
					routes = GeneSelect(routes, routes_Suitability, suitabilitySum);
					CalculateSuitability(routes, routes_Suitability, ref suitabilitySum, ref minSuitability, ref maxSuitability);
					routes = CrossingOver(routes);
					Mutation(routes);
					CalculateSuitability(routes, routes_Suitability, ref suitabilitySum, ref minSuitability, ref maxSuitability);

					// 最善解より良いものが生まれなかったら入れ替え
					var dMin = 1000000000.0;
					var dMinIndex = -1;
					for (int j = 0; j < routes.Length; j++)
					{
						if(CalculateDistance(routes[j]) < dMin)
						{
							dMin = CalculateDistance(routes[j]);
							dMinIndex = j;
						}
					}

					if(dTemp < dMin)
					{
						rTemp.CopyTo(routes[0], 0);
					}
					else
					{
						rTemp = routes[0];
						routes[0] = routes[dMinIndex];
						routes[dMinIndex] = rTemp;
					}


					// それぞれの適合度を計算，最も良いものを表示
					var bestRouteIndex = 0;
					var bestRoute_Suitability = 0.0;
					for (int j = 0; j < routes.Length; j++)
					{
						routes_Suitability[j] = 1 / CalculateDistance(routes[j]);
						if (routes_Suitability[j] > bestRoute_Suitability)
						{
							bestRoute_Suitability = routes_Suitability[j];
							bestRouteIndex = j;
						}
					}

					Dispatcher.Invoke((Action)(() =>
					{
						UpdateImage(routes[bestRouteIndex]);
						distanceLabel.Content = CalculateDistance(routes[bestRouteIndex]) + ", " + bestRouteIndex;
						generationsLabel.Content = i;
					}));
				}
			});
		}



		/// <summary>
		/// 適合度を計算します．
		/// </summary>
		/// <param name="routes"></param>
		/// <param name="routes_Suitability"></param>
		/// <param name="suitabilitySum"></param>
		/// <param name="minSuitability"></param>
		/// <param name="maxSuitability"></param>
		private void CalculateSuitability(int[][] routes, double[] routes_Suitability, ref double suitabilitySum, ref double minSuitability, ref double maxSuitability)
		{
			// 適合度の計算
			for (int i = 0; i < groupSize; i++)
			{
				routes_Suitability[i] = 1 / CalculateDistance(routes[i]);

				if (routes_Suitability[i] < minSuitability)
				{
					minSuitability = routes_Suitability[i];
				}

				if (routes_Suitability[i] > maxSuitability)
				{
					maxSuitability = routes_Suitability[i];
				}
			}

			suitabilitySum = 0.0;

			// 適合度のスケーリング
			for (int i = 0; i < groupSize; i++)
			{
				routes_Suitability[i] = (routes_Suitability[i] - minSuitability) / (maxSuitability - minSuitability) + 1;
				suitabilitySum += routes_Suitability[i];
			}
		}


		/// <summary>
		/// 突然変異
		/// </summary>
		/// <param name="routes"></param>
		private void Mutation(int[][] routes)
		{
			// -----------突然変異-----------
			// ランダムな2点を選択し入れ替え

			// 確率[%]
			var probability = 3;

			for (int i = 0; i < routes.Length; i++)
			{
				// 2点の交差
				if (rand.Next(1, 1000000) <= probability * 10000)
				{
					var a = rand.Next(0, urbanNum - 1);
					var b = rand.Next(0, urbanNum - 1);

					var temp = routes[i][a];
					routes[i][a] = routes[i][b];
					routes[i][b] = temp;
				}

				// 2点間を逆
				if (rand.Next(1, 1000000) <= probability * 10000)
				{
					var startIndex = rand.Next(0, urbanNum - 1);
					var endIndex = rand.Next(startIndex + 1, urbanNum - 1);
					var length = endIndex - startIndex + 1;

					var temp = new int[length];

					for (int j = 0; j < length; j++)
					{
						temp[j] = routes[i][startIndex + j];
					}

					for (int j = 0; j < length; j++)
					{
						routes[i][startIndex + j] = temp[length - j - 1];
					}
				}
			}
		}

		/// <summary>
		/// ルーレット選択
		/// </summary>
		/// <param name="routes"></param>
		/// <param name="routes_Suitability">それぞれの適合度</param>
		/// <param name="suitabilitySum">適合度の合計</param>
		private int[][] GeneSelect(int[][] routes, double[] routes_Suitability, double suitabilitySum)
		{
			// -----------ルーレット選択-----------

			// routeごとの角度の開始点(0-1)
			var routesAngle = new double[routes.Length];

			var angleSum = 0.0;

			for (int i = 0; i < routes.Length; i++)
			{
				routesAngle[i] = angleSum;
				angleSum += routes_Suitability[i] / suitabilitySum;
			}

			// 選択後のroutes
			var selectedRoutes = new int[groupSize][];

			// ルーレット
			for (int i = 0; i < groupSize; i++)
			{
				var r = rand.NextDouble();

				for (int j = groupSize - 1; j >= 0; j--)
				{
					if(routesAngle[j] <= r)
					{
						selectedRoutes[i] = new int[urbanNum];
						routes[j].CopyTo(selectedRoutes[i], 0);
						break;
					}
				}
			}

			return selectedRoutes;
		}

		/// <summary>
		/// ルートの総合距離を計算します．
		/// </summary>
		/// <param name="route"></param>
		/// <returns></returns>
		private double CalculateDistance(int[] route)
		{
			var dis = 0.0;

			for (int i = 0; i < route.Length - 1; i++)
			{
				dis += Math.Sqrt(Math.Pow(citiesData[route[i]].x - citiesData[route[i + 1]].x, 2) + Math.Pow(citiesData[route[i]].y - citiesData[route[i + 1]].y, 2));
			}

			// 終点から始点の距離
			dis += Math.Sqrt(Math.Pow(citiesData[route[route.Length - 1]].x - citiesData[route[0]].x, 2) + Math.Pow(citiesData[route[route.Length - 1]].y - citiesData[route[0]].y, 2));

			return dis;
		}

		/// <summary>
		/// 交叉
		/// </summary>
		/// <param name="routes"></param>
		/// <returns></returns>
		private int[][] CrossingOver(int[][] routes)
		{
			// -----------交叉-----------
			// 交叉前に集団の順番を入れ替える．(同じ対での交叉しか起こらないため)
			// 集団サイズが偶数のときはその前後で行う．奇数のときは最後は実行しない．
			// ex. 集団サイズが4だったとき，0-1，2-3同士で交叉を行う．
			// ex. 集団サイズが5だったとき，0-1, 2-3同士で交叉を行う．4はなにも行わない．

			// 集団の順番をランダムで入れ替え
			routes = routes.OrderBy(i => Guid.NewGuid()).ToArray();

			for (int i = 0; i < routes.Length / 2; i += 2)
			{
				if (rand.Next(0, 10000) > 2000)
				{
					// routes[i]とroutes[i+1]を部分一致交叉
					// まず部分列を決定
					var startIndex = rand.Next(0, urbanNum - (urbanNum / 5) - 1);
					var endIndex = rand.Next(startIndex + 1 + (urbanNum / 5), urbanNum - 1);
					var length = endIndex - startIndex + 1;

					// 部分列の写像
					var mapping = new int[2][];
					mapping[0] = new int[length];
					mapping[1] = new int[length];

					// mappingにコピー
					Array.Copy(routes[i], startIndex, mapping[1], 0, length);
					Array.Copy(routes[i + 1], startIndex, mapping[0], 0, length);


					// 写像部分入れ替え
					// 部分列は除外，写像を参照し存在しなければj++し次へ，存在すれば入れ替えてもう1回
					// (複数回写像での入れ替えが必要な可能性があるため)
					// 写像は，routes[i]がmapping[0]→mapping[1]，routes[i-1]がmapping[1]→mapping[0]

					// routes[i]の入れ替え
					for (int j = 0; j < urbanNum;)
					{
						// 部分列の場合は除外
						if (j >= startIndex && j <= endIndex)
						{
							j += length;
							continue;
						}

						var mapIndex = Array.IndexOf(mapping[0], routes[i][j]);

						if (mapIndex == -1)
						{
							j++;
						}
						else
						{
							routes[i][j] = mapping[1][mapIndex];
						}
					}

					// routes[i+1]の入れ替え
					for (int j = 0; j < urbanNum;)
					{
						// 部分列の場合は除外
						if (j >= startIndex && j <= endIndex)
						{
							j += length;
							continue;
						}

						var mapIndex = Array.IndexOf(mapping[1], routes[i + 1][j]);

						if (mapIndex == -1)
						{
							j++;
						}
						else
						{
							routes[i + 1][j] = mapping[0][mapIndex];
						}
					}

					// 部分列を入れ替え
					mapping[0].CopyTo(routes[i], startIndex);
					mapping[1].CopyTo(routes[i + 1], startIndex);
				}
			}

			return routes;
		}

		/// <summary>
		/// イメージを更新します．
		/// </summary>
		/// <param name="route">通るルート</param>
		private void UpdateImage(int[] route)
		{
			var canvas = new Bitmap(imageSize, imageSize);

			var g = Graphics.FromImage(canvas);

			// 背景を黒に塗りつぶす
			g.FillRectangle(Brushes.Black, 0, 0, imageSize, imageSize);


			// ルートを描画
			var p = new Pen(Color.FromArgb(0, 128, 255));

			for (int i = 0; i < route.Length - 1; i++)
			{
				g.DrawLine(p, citiesData[route[i]].imgX, citiesData[route[i]].imgY, citiesData[route[i + 1]].imgX, citiesData[route[i + 1]].imgY);
			}

			// 最後の都市から最初の都市に戻るルート
			g.DrawLine(p, citiesData[route[route.Length - 1]].imgX, citiesData[route[route.Length - 1]].imgY, citiesData[route[0]].imgX, citiesData[route[0]].imgY);


			// 都市の点を描画
			for (int i = 0; i < citiesData.Length; i++)
			{
				// 都市の色
				var b = new SolidBrush(Color.FromArgb(255, 255, 255));

				g.FillEllipse(b, citiesData[i].imgX, citiesData[i].imgY, 3, 3);
			}

			resultImage.Source = BitmapOperation.ToImageSource(canvas);
		}


		/// <summary>
		/// 都市情報を読み込みます．
		/// </summary>
		/// <param name="FilePath"></param>
		/// <returns></returns>
		public City[] LoadCityData(string FilePath)
		{
			// ファイルが存在しない場合、nullを返す。
			if (!File.Exists(FilePath))
			{
				return null;
			}

			var list = new List<string>();

			using (var file = new StreamReader(FilePath))
			{
				var line = "";
				while ((line = file.ReadLine()) != null)
				{
					if (line != "")
						list.Add(line);
				}
			}

			// 都市情報(1行目はヘッダーのため-1)
			var cities = new City[list.Count - 1];

			for (var i = 1; i < list.Count; i++)
			{
				// インデックス x y
				// の形式
				var s = list[i].Split(' ');

				cities[i - 1].x = double.Parse(s[1]);
				cities[i - 1].y = double.Parse(s[2]);
			}


			// イメージ上の座標の計算

			// 最大値の探索
			var max = 0.0;

			for (int i = 0; i < cities.Length; i++)
			{
				if (cities[i].x > max)
				{
					max = cities[i].x;
				}

				if (cities[i].y > max)
				{
					max = cities[i].y;
				}
			}

			// イメージの倍率．座標*倍率=イメージ上の座標
			var scaleFactor = imageSize / max;

			for (int i = 0; i < cities.Length; i++)
			{
				cities[i].imgX = (int)(cities[i].x * scaleFactor);
				cities[i].imgY = (int)(cities[i].y * scaleFactor);
			}

			return cities;
		}
	}


	static class BitmapOperation
	{
		[DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject([In] IntPtr hObject);

		/// <summary>
		/// Bitmap型をImageSource型に変換します．
		/// </summary>
		/// <param name="bmp"></param>
		/// <returns></returns>
		public static ImageSource ToImageSource(this Bitmap bmp)
		{
			var handle = bmp.GetHbitmap();
			try
			{
				return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			}
			finally { DeleteObject(handle); }
		}
	}
}
