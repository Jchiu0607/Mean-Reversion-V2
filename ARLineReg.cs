#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class ARLineReg : Indicator
	{
		#region Internal Series and List		
		private Series<double> TransformedSeries;
		private Series<double> TransLinReg;
		private Series<double> TransSumSq;
		private Series<double> TransMean;
		private Series<double> TransVar;
		private Series<double> RunningMean;
		private Series<double> RunningVariance;
		private Series<double> SumOfSquaresAvg;
		private Series<double> Residuals;
		private Series<double> ResMean;
		private Series<double> ResSumOfSquares;
		private Series<double> ResVariance;
		private List<Series<double>> Coefficients;		
		#endregion
		#region Internal Variables
		private double alpha;
		private Brush tempBrushes;
		#endregion
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Auto-regression on Z-scored data of order Period (i.e. AR(Period) model) using a modified perceptron-style gradient descent method to determine the regression Coefficients. The learning rate decreases over time.";
				Name										= "ARLineReg";
				Calculate									= Calculate.OnEachTick;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				BarsRequiredToPlot 							= 10;						
				IsSuspendedWhileInactive					= true;
				IsAutoScale									= false;
				Period										= 13;
				Coeff										= 0;
				Predict										= false;
				PredictBrushes								= Brushes.Blue;
				Predict50Brushes							= Brushes.Green;
				Predict90Brushes							= Brushes.Purple;
				Predict95Brushes							= Brushes.Red;
				alpha										= .005;
				AddPlot(Brushes.Yellow, "LinRegVal");
    			AddPlot(Brushes.Red, "LinRegCoef");
				
			}
			else if (State == State.Configure)
			{
				
			}
			else if (State == State.DataLoaded)
			{				
				TransformedSeries 	= new Series<double>(this);
				TransLinReg 		= new Series<double>(this);
				TransSumSq 			= new Series<double>(this);
				TransMean 			= new Series<double>(this);
				TransVar			= new Series<double>(this);
				RunningMean 		= new Series<double>(this);
				RunningVariance		= new Series<double>(this);
				SumOfSquaresAvg		= new Series<double>(this);
				Residuals 			= new Series<double>(this);
				ResMean				= new Series<double>(this);
				ResSumOfSquares		= new Series<double>(this);
				ResVariance			= new Series<double>(this);
				Coefficients		= new List<Series<double>>(Period);
				for(int i = 0; i<Period ; i++)
				{
					Coefficients.Add(new Series<double>(this,MaximumBarsLookBack.Infinite));
				}
			}
		}

		protected override void OnBarUpdate()
		{
			#region Initialize Coefficients, and prevent divergence of learning algo
			//Note, this is required as without it it may throw off convergence of the learning algorithm
			if ( CurrentBar < Math.Max(BarsRequiredToPlot,Period))
			{
				for( int k = 0; k<Period; k++)
				{
					Coefficients[k][0] =(1/(Period));
				}
				
				if ( CurrentBar == 0 )
				{
					tempBrushes = PlotBrushes[0][0]; //get the original Brushes
				}
				
				return;
			}
			#endregion
			
			#region Avoid plotting during convergence Period
			//This is done to avoid plotting the large swings at the beginning while the learning algo is converging
			if ( CurrentBar < Math.Max(BarsRequiredToPlot,Period) + 30)
			{
				PlotBrushes[0][0] = Brushes.Transparent;
			}
			else if ( CurrentBar == Math.Max(BarsRequiredToPlot,Period)+30 )
			{
				PlotBrushes[0][0] = tempBrushes; //assign plot to original Brushes
			}
			#endregion
			
			#region Learning Rate
			//Set the learning rate
			if ( IsFirstTickOfBar )
			{
				alpha = 1/(Period*Math.Sqrt(CurrentBar));  //Set the learning rate
				////Print("Learning Rate : "+alpha);
			}
			#endregion
			
			#region Get the time-series mean and variance for Z-scoring process
			//Set the time-series mean and variance
			if (IsFirstTickOfBar)
			{
       			double lastMean = RunningMean[1]*CurrentBar;
				double lastSqSumAvg  = SumOfSquaresAvg[1]*CurrentBar;
				
				RunningMean[0] =( (lastMean + Input[0]) / (CurrentBar+1) );
				
				SumOfSquaresAvg[0] =( (lastSqSumAvg + Input[0]*Input[0]) / (CurrentBar+1) );
				RunningVariance[0] =( SumOfSquaresAvg[0] - RunningMean[0]*RunningMean[0]);
				
				// Make sure variance isn't 0, set to 1 if it is
				if(RunningVariance[0] == 0)
				{
					RunningVariance[0] =(1);
				}
				////Print the mean and variance for the main series
				//Print("Input Mean "+RunningMean[0]+" Variance "+RunningVariance[0]);
			}
			#endregion
			
			#region Get the transformed (Z-score) series, and its mean and variance
			//Set the transformed series
			if(IsFirstTickOfBar)
			{
				TransformedSeries[0] =((Input[0]-RunningMean[0])/Math.Sqrt(RunningVariance[0]));
			
				//Get the transformed series mean and variance
       			double translastMean = TransMean[1]*CurrentBar;
				double translastSqSumAvg  = TransSumSq[1]*CurrentBar;
				
				TransMean[0] =( (translastMean + TransformedSeries[0]) / (CurrentBar+1) );
				
				TransSumSq[0] =( (translastSqSumAvg + TransformedSeries[0]*TransformedSeries[0]) / (CurrentBar+1) );
				TransVar[0] =( TransSumSq[0] - TransMean[0]*TransMean[0]);
				////Print the mean and variance for the main series
				//Print("Transformed Series : "+TransformedSeries[0]+" Mean "+TransMean[0]+" Variance "+TransVar[0]);
			}
			#endregion
		
			#region Set the predicted next auto-regressive value
			//Set the AR Linear regression
			if ( IsFirstTickOfBar )
			{
				// Get the new linear regression calculation based on past Coefficients
				// Note that there is no need for "intercept" constant as we are using z-scores, i.e. we have 0 mean => constant = (1-sum_of_coeff)*mean = 0
				double LinRegValue = 0;
				for ( int j = 0; j<Period ; j++)
				{
					LinRegValue = LinRegValue + Coefficients[j][1]*TransformedSeries[j+1];
				}
			
				// Set the transformed linear regression and the plot
				TransLinReg[0] =(LinRegValue);
				Values[0][0] =(Math.Sqrt(RunningVariance[0])*TransLinReg[0]+RunningMean[0]);
				
				//Print("Linear regression value at bar "+CurrentBar+" is : "+TransLinReg[0]);
			}
			#endregion
			
			#region Coefficient update, perceptron learning algo
			if(IsFirstTickOfBar)
			{
				for ( int i = 0; i<Period ; i++)
				{
					Coefficients[i][0] =(Coefficients[i][1]-alpha*TransformedSeries[i+1]*(TransLinReg[1]-TransformedSeries[1]));
					//Print("Coefficient B"+i+" is "+Coefficients[i][0]);
				}

				//Set the coefficient series (so one can plot any coefficient)
				if ( Coeff >= Period )
				{
					Values[1][0] =(Coefficients[Period-1][0]);
				}
				else
				{
					Values[1][0] =(Coefficients[Coeff][0]);
				}
			}
			#endregion
			
			#region Get the Residuals, and their mean and variance for error calculation
			//Get the Residuals, and the residual mean and variance
			if(IsFirstTickOfBar)
			{
				if ( CurrentBar < (Math.Max(Period,BarsRequiredToPlot)+30))  //This is done to allow some convergence of the AR lin reg before we estimate errors
				{
					Residuals[0] =(0);
					ResMean[0] =(0);
					ResSumOfSquares[0] =(0);
					ResVariance[0] =(0);
				}
				else
				{
					Residuals[0] =(Input[1]-Values[0][1]);
					ResMean[0] =( ((CurrentBar)*ResMean[1] + Residuals[0])/(CurrentBar+1) );
					ResSumOfSquares[0] =( ((CurrentBar)*ResSumOfSquares[1] + Residuals[0]*Residuals[0])/(CurrentBar+1) );
					ResVariance[0] =( ResSumOfSquares[0] - ResMean[0]*ResMean[0]);
					//Print("Residual Mean : "+ResMean[0]+" Variance : "+ResVariance[0]);
					//Print(" ");
				}
			}
			#endregion
			
			#region Prediction error plotting
			if ( Predict == true && State != State.Historical)
			{
				double Price = 0;
				for (int p = 0; p<Period ; p++)
				{
					Price = Price + TransformedSeries[p+1]*Coefficients[p][0];
				}
				
				
				Price = Price*Math.Sqrt(RunningVariance[0]) + RunningMean[0];
				
				Print(CurrentBar+" Price= "+Price+" ResVariance= "+ResVariance);
				
				Draw.Text(this,"Mid"+CurrentBar.ToString(),"*",0,Price,PredictBrushes);
				Draw.Text(this,"Upper50"+CurrentBar.ToString(),"*",0,Price+0.674*Math.Sqrt(ResVariance[0]),Predict50Brushes);
				Draw.Text(this,"Lower50"+CurrentBar.ToString(),"*",0,Price-0.674*Math.Sqrt(ResVariance[0]),Predict50Brushes);
				Draw.Text(this,"Upper90"+CurrentBar.ToString(),"*",0,Price+1.644*Math.Sqrt(ResVariance[0]),Predict90Brushes);
				Draw.Text(this,"Lower90"+CurrentBar.ToString(),"*",0,Price-1.644*Math.Sqrt(ResVariance[0]),Predict90Brushes);
				Draw.Text(this,"Upper95"+CurrentBar.ToString(),"*",0,Price+1.959*Math.Sqrt(ResVariance[0]),Predict95Brushes);
				Draw.Text(this,"Lower95"+CurrentBar.ToString(),"*",0,Price-1.959*Math.Sqrt(ResVariance[0]),Predict95Brushes);
				
				Draw.Ray(this,"Mid",false,0,Price,-1,Price,PredictBrushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Upper50",true,0,Price+0.674*Math.Sqrt(ResVariance[0]),-1,Price+0.674*Math.Sqrt(ResVariance[0]),Predict50Brushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Lower50",true,0,Price-0.674*Math.Sqrt(ResVariance[0]),-1,Price-0.674*Math.Sqrt(ResVariance[0]),Predict50Brushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Upper90",true,0,Price+1.644*Math.Sqrt(ResVariance[0]),-1,Price+1.644*Math.Sqrt(ResVariance[0]),Predict90Brushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Lower90",true,0,Price-1.644*Math.Sqrt(ResVariance[0]),-1,Price-1.644*Math.Sqrt(ResVariance[0]),Predict90Brushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Upper95",true,0,Price+1.959*Math.Sqrt(ResVariance[0]),-1,Price+1.959*Math.Sqrt(ResVariance[0]),Predict95Brushes,DashStyleHelper.Dash,2);
				Draw.Ray(this,"Lower95",true,0,Price-1.959*Math.Sqrt(ResVariance[0]),-1,Price-1.959*Math.Sqrt(ResVariance[0]),Predict95Brushes,DashStyleHelper.Dash,2);
			}
			#endregion
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Period", Order=1, GroupName="Parameters")]
		public int Period
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name="Coeff", Order=2, GroupName="Parameters")]
		public int Coeff
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Predict", Order=3, GroupName="Parameters")]
		public bool Predict
		{ get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="PredictBrushes", Order=4, GroupName="Parameters")]
		public Brush PredictBrushes
		{ get; set; }

		[Browsable(false)]
		public string PredictBrushesSerializable
		{
			get { return Serialize.BrushToString(PredictBrushes); }
			set { PredictBrushes = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Predict50Brushes", Order=5, GroupName="Parameters")]
		public Brush Predict50Brushes
		{ get; set; }

		[Browsable(false)]
		public string Predict50BrushesSerializable
		{
			get { return Serialize.BrushToString(Predict50Brushes); }
			set { Predict50Brushes = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Predict90Brushes", Order=6, GroupName="Parameters")]
		public Brush Predict90Brushes
		{ get; set; }

		[Browsable(false)]
		public string Predict90BrushesSerializable
		{
			get { return Serialize.BrushToString(Predict90Brushes); }
			set { Predict90Brushes = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Predict95Brushes", Order=7, GroupName="Parameters")]
		public Brush Predict95Brushes
		{ get; set; }

		[Browsable(false)]
		public string Predict95BrushesSerializable
		{
			get { return Serialize.BrushToString(Predict95Brushes); }
			set { Predict95Brushes = Serialize.StringToBrush(value); }
		}			
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ARLineReg[] cacheARLineReg;
		public ARLineReg ARLineReg(int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			return ARLineReg(Input, period, coeff, predict, predictBrushes, predict50Brushes, predict90Brushes, predict95Brushes);
		}

		public ARLineReg ARLineReg(ISeries<double> input, int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			if (cacheARLineReg != null)
				for (int idx = 0; idx < cacheARLineReg.Length; idx++)
					if (cacheARLineReg[idx] != null && cacheARLineReg[idx].Period == period && cacheARLineReg[idx].Coeff == coeff && cacheARLineReg[idx].Predict == predict && cacheARLineReg[idx].PredictBrushes == predictBrushes && cacheARLineReg[idx].Predict50Brushes == predict50Brushes && cacheARLineReg[idx].Predict90Brushes == predict90Brushes && cacheARLineReg[idx].Predict95Brushes == predict95Brushes && cacheARLineReg[idx].EqualsInput(input))
						return cacheARLineReg[idx];
			return CacheIndicator<ARLineReg>(new ARLineReg(){ Period = period, Coeff = coeff, Predict = predict, PredictBrushes = predictBrushes, Predict50Brushes = predict50Brushes, Predict90Brushes = predict90Brushes, Predict95Brushes = predict95Brushes }, input, ref cacheARLineReg);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ARLineReg ARLineReg(int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			return indicator.ARLineReg(Input, period, coeff, predict, predictBrushes, predict50Brushes, predict90Brushes, predict95Brushes);
		}

		public Indicators.ARLineReg ARLineReg(ISeries<double> input , int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			return indicator.ARLineReg(input, period, coeff, predict, predictBrushes, predict50Brushes, predict90Brushes, predict95Brushes);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ARLineReg ARLineReg(int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			return indicator.ARLineReg(Input, period, coeff, predict, predictBrushes, predict50Brushes, predict90Brushes, predict95Brushes);
		}

		public Indicators.ARLineReg ARLineReg(ISeries<double> input , int period, int coeff, bool predict, Brush predictBrushes, Brush predict50Brushes, Brush predict90Brushes, Brush predict95Brushes)
		{
			return indicator.ARLineReg(input, period, coeff, predict, predictBrushes, predict50Brushes, predict90Brushes, predict95Brushes);
		}
	}
}

#endregion
