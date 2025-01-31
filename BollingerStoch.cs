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
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators.AUN_Indi.Ehlers;

#endregion

//This namespace holds Strategies in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Strategies
{
      public class BollingerStoch : Strategy
      {


            protected override void OnStateChange()
            {
                  if (State == State.SetDefaults)
                  {
                        Description                                                 = @"trade standard deviations";
                        Name                                                        = "BollingerStoch";
                        Calculate                                                   = Calculate.OnEachTick;
                        EntriesPerDirection                                         = 1;
                        EntryHandling                                               = EntryHandling.AllEntries;
                        IsExitOnSessionCloseStrategy                    = true;
                        ExitOnSessionCloseSeconds                             = 30;
                        IsFillLimitOnTouch                                          = false;
                        MaximumBarsLookBack                                         = MaximumBarsLookBack.TwoHundredFiftySix;
                        OrderFillResolution                                         = OrderFillResolution.Standard;
                        Slippage                                                    = 0;
                        StartBehavior                                               = StartBehavior.WaitUntilFlat;
                        TimeInForce                                                 = TimeInForce.Gtc;
                        TraceOrders                                                 = false;
                        RealtimeErrorHandling                                 = RealtimeErrorHandling.StopCancelClose;
                        StopTargetHandling                                          = StopTargetHandling.PerEntryExecution;
                        BarsRequiredToTrade                                         = 20;
                        // Disable this property for performance gains in Strategy Analyzer optimizations
                        // See the Help Guide for additional information
                        IsInstantiatedOnEachOptimizationIteration = true;
                        BollingerPeriod                           = 170; // optimized NQ: to 100 - ES: 160 - YM: 190
                        SLPeriod                                  = 9; // optimized NQ: to 9 - ES: 12 - YM: 11
                        SLMultiple                                = 3; // optimized NQ: to 4.5 - ES: 4 - YM: 4.5
						LongBandwidthMult						  = 2;
						ShortBandwidthMult						  = 1;	
						TP2Mult									  = 2;
				
                        
                        
                  }
                  else if (State == State.Configure)
                  {

                  }
                  if (State == State.DataLoaded)
                  {
                        Bollinger(2,BollingerPeriod).Plots[0].Brush = Brushes.Red;
                        Bollinger(2,BollingerPeriod).Plots[1].Brush = Brushes.BlueViolet;
                        Bollinger(2,BollingerPeriod).Plots[2].Brush = Brushes.Green;
                        AddChartIndicator(Bollinger(2,BollingerPeriod));
                        //AddChartIndicator(MACD(12,26,9));
                  }
            }

            protected override void OnBarUpdate()
            {
                   if (CurrentBar < 100)
        return;
                  
                  

            // Get Bollinger Bands and Stochastic values
            double upperBand = Bollinger(2, BollingerPeriod).Upper[0];
            double lowerBand = Bollinger(2, BollingerPeriod).Lower[0];
            double middleBand = Bollinger(2, BollingerPeriod).Middle[0];
            double bandwidth = (upperBand - lowerBand) / middleBand;


            // Calculate stop loss and take profit levels
            double stopLossPrice;
            double targetPrice;
			double targetPrice2;
      
      

      // Long Condition
      if (Close[1] < Bollinger(2, BollingerPeriod).Lower[1] && Close[1] < Open[1] && Close[0] > Open[1] && Close[0] > Bollinger(2, BollingerPeriod).Lower[0] && Volume[0] > VOLMA(10)[0])
      {
            stopLossPrice = Low[LowestBar(Low, SLPeriod)];
            targetPrice = Close[0] + SLMultiple * (Close[0] - stopLossPrice);
			targetPrice2 = Close[0] + (SLMultiple / TP2Mult) * (Close[0] - stopLossPrice);

			
            if (bandwidth < 0.01)
		{
                  targetPrice = Close[0] + LongBandwidthMult * (Close[0] - stopLossPrice);
				  targetPrice2 = Close[0] + (LongBandwidthMult / 2) * (Close[0] - stopLossPrice);
		}

            EnterLong("Bollinger Long1");
			//EnterLong("Bollinger Long2");
            SetStopLoss(CalculationMode.Price, stopLossPrice);
            SetProfitTarget("Bollinger Long1", CalculationMode.Price, targetPrice, false);
			SetProfitTarget("Bollinger Long2", CalculationMode.Price, targetPrice2, false);
		
      }

      // Short Condition
      if (Close[1] > Bollinger(2, BollingerPeriod).Upper[1] && Close[1] > Open[1] && Close[0] < Open[1] && Close[0] < Bollinger(2, BollingerPeriod).Upper[0] && ChoppinessIndex(10)[0] > 60 && Volume[0] > VOLMA(10)[0])
            {
            stopLossPrice = High[HighestBar(High, SLPeriod)];
            targetPrice = Close[0] - SLMultiple * (stopLossPrice - Close[0]);
			targetPrice2 = Close[0] - (SLMultiple / TP2Mult) * (stopLossPrice - Close[0]);

            if (bandwidth < 0.01)
		{
                targetPrice = Close[0] - ShortBandwidthMult * (stopLossPrice - Close[0]);
				targetPrice2 = Close[0] - (ShortBandwidthMult / 2) * (stopLossPrice - Close[0]);
		}


            EnterShort("Bollinger Short1");
			//EnterShort("Bollinger Short2");
            SetStopLoss(CalculationMode.Price, stopLossPrice);
            SetProfitTarget("Bollinger Short1", CalculationMode.Price, targetPrice, false);
			SetProfitTarget("Bollinger Short2", CalculationMode.Price, targetPrice2, false);

                  }
	

      
            }



            
      


            
            #region Properties
            [NinjaScriptProperty]
            [Range(1, int.MaxValue)]
            [Display(Name="BollingerPeriod", Order=1, GroupName="Parameters")]
            public int BollingerPeriod
            { get; set; }
            
            [NinjaScriptProperty]
            [Range(1, int.MaxValue)]
            [Display(Name="SLPeriod", Order=2, GroupName="Parameters")]
            public int SLPeriod
            { get; set; }
            
            [NinjaScriptProperty]
            [Range(0.5, double.MaxValue)]
            [Display(Name="SLMultiple", Order=3, GroupName="Parameters")]
            public double SLMultiple
            { get; set; }

			[NinjaScriptProperty]
            [Range(1, int.MaxValue)]
            [Display(Name="LongBandwidthMult", Order=4, GroupName="Parameters")]
            public int LongBandwidthMult
            { get; set; }

			[NinjaScriptProperty]
            [Range(1, int.MaxValue)]
            [Display(Name="ShortBandwidthMult", Order=4, GroupName="Parameters")]
            public int ShortBandwidthMult
            { get; set; }

			[NinjaScriptProperty]
            [Range(0.5, double.MaxValue)]
            [Display(Name="TP2Mult", Order=4, GroupName="Parameters")]
            public double TP2Mult
            { get; set; }
            #endregion
      }
}