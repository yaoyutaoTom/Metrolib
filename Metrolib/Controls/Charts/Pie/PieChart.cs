﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable CheckNamespace

namespace Metrolib
// ReSharper restore CheckNamespace
{
	/// <summary>
	/// </summary>
	public class PieChart
		: Canvas
	{
		/// <summary>
		///     Definition of the <see cref="Series" /> dependency property.
		/// </summary>
		public static readonly DependencyProperty SeriesProperty =
			DependencyProperty.Register("Series", typeof (IPieSeries), typeof (PieChart),
			                            new PropertyMetadata(null, OnSeriesChanged));

		/// <summary>
		///     Definition of the <see cref="ValueTemplate" /> dependency property.
		/// </summary>
		public static readonly DependencyProperty ValueTemplateProperty =
			DependencyProperty.Register("ValueTemplate", typeof (DataTemplate), typeof (PieChart),
			                            new PropertyMetadata(default(DataTemplate)));

		/// <summary>
		///     Definition of the <see cref="LabelTemplate" />
		/// </summary>
		public static readonly DependencyProperty LabelTemplateProperty =
			DependencyProperty.Register("LabelTemplate", typeof (DataTemplate), typeof (PieChart),
			                            new PropertyMetadata(default(DataTemplate)));

		private static readonly DependencyPropertyKey SumOfSlicesPropertyKey
			= DependencyProperty.RegisterReadOnly("SumOfSlices", typeof (double), typeof (PieChart),
			                                      new FrameworkPropertyMetadata(default(double),
			                                                                    FrameworkPropertyMetadataOptions.None));

		/// <summary>
		///     Definition of the <see cref="Outline" /> dependency property.
		/// </summary>
		public static readonly DependencyProperty OutlineProperty =
			DependencyProperty.Register("Outline", typeof (Pen), typeof (PieChart), new PropertyMetadata(default(Pen)));

		/// <summary>
		///     Definition of the <see cref="LabelMargin" /> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelMarginProperty =
			DependencyProperty.Register("LabelMargin", typeof (double), typeof (PieChart), new PropertyMetadata(8.0));

		/// <summary>
		///     Definition of the <see cref="SumOfSlices" /> dependency property.
		/// </summary>
		public static readonly DependencyProperty SumOfSlicesProperty
			= SumOfSlicesPropertyKey.DependencyProperty;

		private readonly Dictionary<IPieSlice, PieChartSliceItem> _sliceItems;
		private readonly Dictionary<IPieSlice, PieChartLabelItem> _titleItems;
		private readonly Dictionary<IPieSlice, PieChartValueItem> _valueItems;

		private bool _isLoaded;
		private IEnumerable<IPieSlice> _slices;

		static PieChart()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof (PieChart), new FrameworkPropertyMetadata(typeof (PieChart)));
		}

		/// <summary>
		///     Initializes this chart.
		/// </summary>
		public PieChart()
		{
			_sliceItems = new Dictionary<IPieSlice, PieChartSliceItem>();
			_titleItems = new Dictionary<IPieSlice, PieChartLabelItem>();
			_valueItems = new Dictionary<IPieSlice, PieChartValueItem>();

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			SizeChanged += OnSizeChanged;
		}

		/// <summary>
		///     The data template, if any, used to present a <see cref="IPieSlice.DisplayedValue" />.
		/// </summary>
		public DataTemplate ValueTemplate
		{
			get { return (DataTemplate) GetValue(ValueTemplateProperty); }
			set { SetValue(ValueTemplateProperty, value); }
		}

		/// <summary>
		///     The data template, if any, used to present a <see cref="IPieSlice.Label" />.
		/// </summary>
		public DataTemplate LabelTemplate
		{
			get { return (DataTemplate) GetValue(LabelTemplateProperty); }
			set { SetValue(LabelTemplateProperty, value); }
		}

		/// <summary>
		///     The margin between the pie chart's outline and the label that is placed next to each slice.
		/// </summary>
		public double LabelMargin
		{
			get { return (double) GetValue(LabelMarginProperty); }
			set { SetValue(LabelMarginProperty, value); }
		}

		/// <summary>
		///     The outline used to draw *around* the entire bar chart.
		/// </summary>
		public Pen Outline
		{
			get { return (Pen) GetValue(OutlineProperty); }
			set { SetValue(OutlineProperty, value); }
		}

		/// <summary>
		///     The sum of the individual values of all slices.
		/// </summary>
		public double SumOfSlices
		{
			get { return (double) GetValue(SumOfSlicesProperty); }
			protected set { SetValue(SumOfSlicesPropertyKey, value); }
		}

		/// <summary>
		///     The series displayed by this chart.
		/// </summary>
		public IPieSeries Series
		{
			get { return (IPieSeries) GetValue(SeriesProperty); }
			set { SetValue(SeriesProperty, value); }
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs args)
		{
			MakeDirty();
		}

		private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
		{
			if (_isLoaded)
				return;

			SubscribeTo(Series);
			MakeDirty();

			_isLoaded = true;
		}

		private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs)
		{
			UnsubscribeFrom(Series);
			_isLoaded = false;
		}

		private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((PieChart) d).OnSeriesChanged((IPieSeries) e.OldValue, (IPieSeries) e.NewValue);
		}

		private void OnSeriesChanged(IPieSeries oldValue, IPieSeries newValue)
		{
			if (_isLoaded)
			{
				UnsubscribeFrom(oldValue);
				SubscribeTo(newValue);
			}
		}

		private void ClearItems()
		{
			foreach (PieChartSliceItem item in _sliceItems.Values)
			{
				InternalChildren.Remove(item);
			}
			_sliceItems.Clear();

			foreach (var item in _valueItems.Values)
			{
				InternalChildren.Remove(item);
			}
			_valueItems.Clear();

			foreach (PieChartLabelItem item in _titleItems.Values)
			{
				InternalChildren.Remove(item);
			}
			_titleItems.Clear();
		}

		private void SubscribeTo(IPieSeries series)
		{
			var notifiable = series as INotifyPropertyChanged;
			if (notifiable != null)
			{
				notifiable.PropertyChanged += SeriesOnPropertyChanged;
			}

			if (series != null)
			{
				SubscribeTo(series.Slices);
				AddItems(series.Slices);
				_slices = series.Slices;
			}
			else
			{
				_slices = null;
			}

			SumOfSlices = _slices != null
				              ? _slices.Sum(x => x.Value)
				              : 0;
		}

		private void AddItems(IEnumerable<IPieSlice> slices)
		{
			if (slices == null)
				return;

			foreach (IPieSlice slice in slices)
			{
				OnSliceAdded(slice);
			}
		}

		private void OnSliceAdded(IPieSlice slice)
		{
			var titleItem = new PieChartLabelItem
				{
					Content = slice.Label,
					ContentTemplate = LabelTemplate
				};
			_titleItems.Add(slice, titleItem);
			InternalChildren.Add(titleItem);

			var valueItem = new PieChartValueItem
				{
					Content = slice.DisplayedValue,
					ContentTemplate = ValueTemplate
				};
			_valueItems.Add(slice, valueItem);
			InternalChildren.Add(valueItem);

			var sliceItem = new PieChartSliceItem
				{
					Slice = slice
				};
			_sliceItems.Add(slice, sliceItem);
			InternalChildren.Add(sliceItem);
		}

		private void SeriesOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			switch (args.PropertyName)
			{
				case "Slices":
					UnsubscribeFrom(_slices);
					ClearItems();
					SubscribeTo(Series.Slices);
					_slices = Series.Slices;
					AddItems(_slices);
					break;
			}
		}

		private void UnsubscribeFrom(IPieSeries series)
		{
			var notifiable = series as INotifyPropertyChanged;
			if (notifiable != null)
			{
				notifiable.PropertyChanged -= SeriesOnPropertyChanged;
			}

			if (series != null)
			{
				UnsubscribeFrom(series.Slices);
			}
			ClearItems();
		}

		private void SubscribeTo(IEnumerable<IPieSlice> slices)
		{
			var notifiable = slices as INotifyCollectionChanged;
			if (notifiable != null)
			{
				notifiable.CollectionChanged += SlicesOnCollectionChanged;
			}
		}

		private void SlicesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			MakeDirty();
		}

		private void MakeDirty()
		{
			InvalidateVisual();
		}

		private void UnsubscribeFrom(IEnumerable<IPieSlice> slices)
		{
			var notifiable = slices as INotifyCollectionChanged;
			if (notifiable != null)
			{
				notifiable.CollectionChanged -= SlicesOnCollectionChanged;
			}
		}

		/// <summary>
		///     Arranges the content of a System.Windows.Controls.Canvas element.
		/// </summary>
		/// <param name="arrangeSize"></param>
		/// <returns></returns>
		protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeSize)
		{
			if (_slices != null)
			{
				double radius = Math.Min(ActualWidth/2, ActualHeight/2);
				double startAngle = 0;

				foreach (IPieSlice slice in _slices)
				{
					PieChartSliceItem item = _sliceItems[slice];

					double relativeValue = item.Slice.Value/SumOfSlices;
					double openAngle = relativeValue*2*Math.PI;

					item.StartAngle = startAngle;
					item.OpenAngle = openAngle;
					item.Radius = radius;

					startAngle += openAngle;
				}
			}

			var offset = new Vector(ActualWidth/2, ActualHeight/2);
			foreach (var pair in _titleItems)
			{
				IPieSlice slice = pair.Key;
				PieChartLabelItem item = pair.Value;
				PieChartSliceItem sliceItem = _sliceItems[slice];
				System.Windows.Size desiredSize = item.DesiredSize;

				double angle = sliceItem.StartAngle + sliceItem.OpenAngle/2;
				Vector specificOffset;
				switch (sliceItem.Direction)
				{
					case SliceDirection.TopRight:
						specificOffset = new Vector(0, -desiredSize.Height);
						break;

					case SliceDirection.BottomRight:
						specificOffset = new Vector(0, 0);
						break;

					case SliceDirection.BottomLeft:
						specificOffset = new Vector(-desiredSize.Width, 0);
						break;

					default:
						specificOffset = new Vector(-desiredSize.Width, -desiredSize.Height);
						break;
				}

				Point position = GetPoint(sliceItem.Radius + LabelMargin, angle)
				                 + offset
				                 + specificOffset;

				var rect = new Rect(position, desiredSize);
				item.Arrange(rect);
			}

			foreach (var pair in _valueItems)
			{
				IPieSlice slice = pair.Key;
				PieChartValueItem item = pair.Value;
				PieChartSliceItem sliceItem = _sliceItems[slice];
				
				System.Windows.Size desiredSize = item.DesiredSize;
				double angle = sliceItem.StartAngle + sliceItem.OpenAngle/2;
				var specificOffset = -(Vector) desiredSize/2;

				Point position = GetPoint(sliceItem.Radius/1.5, angle)
				                 + offset
				                 + specificOffset;

				var rect = new Rect(position, desiredSize);
				item.Arrange(rect);
			}

			return arrangeSize;
		}

		/// <summary>
		///     Draws the content of a System.Windows.Media.DrawingContext object during the render pass of a System.Windows.Controls.Panel element.
		/// </summary>
		/// <param name="drawingContext"></param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			double radius = Math.Min(ActualWidth/2, ActualHeight/2);
			var offset = new Vector(ActualWidth/2, ActualHeight/2);
			var center = new Point(0, 0);

			if (_slices != null)
			{
				drawingContext.PushTransform(new TranslateTransform(offset.X, offset.Y));
				try
				{
					var maxThickness = 0.0;

					foreach (PieChartSliceItem item in _sliceItems.Values)
					{
						double startAngle = item.StartAngle;
						double openAngle = item.OpenAngle;
						var geometry = new StreamGeometry();
						var pen = item.Slice.Outline;
						bool isStroked = pen != null;

						if (pen != null)
							maxThickness = Math.Max(maxThickness, pen.Thickness);

						using (StreamGeometryContext context = geometry.Open())
						{
							context.BeginFigure(center, true, true);
							context.LineTo(GetPoint(radius, startAngle), isStroked, false);
							context.ArcTo(GetPoint(radius, startAngle + openAngle),
							              new System.Windows.Size(radius, radius),
							              startAngle*180/Math.PI,
							              openAngle > Math.PI,
							              SweepDirection.Clockwise,
							              isStroked,
							              false);
							context.LineTo(center, isStroked, false);
						}

						drawingContext.DrawGeometry(item.Slice.Fill, pen, geometry);
					}

					if (Outline != null)
					{
						drawingContext.DrawEllipse(null,
						                           Outline,
						                           center,
						                           radius + maxThickness,
						                           radius + maxThickness);
					}
				}
				finally
				{
					drawingContext.Pop();
				}
			}
		}

		private static Point GetPoint(double radius, double angle)
		{
			return new Point(-Math.Sin(angle)*radius,
			                 Math.Cos(angle)*radius);
		}
	}
}