﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using OpenTK;
using osu.Game.Modes.Objects.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Game.Beatmaps.Formats;
using osu.Game.Audio;

namespace osu.Game.Modes.Objects.Legacy
{
    /// <summary>
    /// A HitObjectParser to parse legacy Beatmaps.
    /// </summary>
    internal abstract class HitObjectParser : Objects.HitObjectParser
    {
        public override HitObject Parse(string text)
        {
            string[] split = text.Split(',');
            var type = (HitObjectType)int.Parse(split[3]) & ~HitObjectType.ColourHax;
            bool combo = type.HasFlag(HitObjectType.NewCombo);
            type &= ~HitObjectType.NewCombo;

            int sampleVolume = 0;
            string normalSampleBank = null;
            string addSampleBank = null;

            HitObject result;

            if ((type & HitObjectType.Circle) > 0)
            {
                result = CreateHit(new Vector2(int.Parse(split[0]), int.Parse(split[1])), combo);

                if (split.Length > 5)
                    readCustomSampleBanks(split[5], ref normalSampleBank, ref addSampleBank, ref sampleVolume);
            }
            else if ((type & HitObjectType.Slider) > 0)
            {
                CurveType curveType = CurveType.Catmull;
                double length = 0;
                var points = new List<Vector2> { new Vector2(int.Parse(split[0]), int.Parse(split[1])) };

                string[] pointsplit = split[5].Split('|');
                foreach (string t in pointsplit)
                {
                    if (t.Length == 1)
                    {
                        switch (t)
                        {
                            case @"C":
                                curveType = CurveType.Catmull;
                                break;
                            case @"B":
                                curveType = CurveType.Bezier;
                                break;
                            case @"L":
                                curveType = CurveType.Linear;
                                break;
                            case @"P":
                                curveType = CurveType.PerfectCurve;
                                break;
                        }
                        continue;
                    }

                    string[] temp = t.Split(':');
                    points.Add(new Vector2((int)Convert.ToDouble(temp[0], CultureInfo.InvariantCulture), (int)Convert.ToDouble(temp[1], CultureInfo.InvariantCulture)));
                }

                int repeatCount = Convert.ToInt32(split[6], CultureInfo.InvariantCulture);

                if (repeatCount > 9000)
                    throw new ArgumentOutOfRangeException(nameof(repeatCount), @"Repeat count is way too high");

                if (split.Length > 7)
                    length = Convert.ToDouble(split[7], CultureInfo.InvariantCulture);

                result = CreateSlider(new Vector2(int.Parse(split[0]), int.Parse(split[1])), combo, points, length, curveType, repeatCount);

                if (split.Length > 10)
                    readCustomSampleBanks(split[10], ref normalSampleBank, ref addSampleBank, ref sampleVolume);
            }
            else if ((type & HitObjectType.Spinner) > 0)
            {
                result = CreateSpinner(new Vector2(512, 384) / 2, Convert.ToDouble(split[5], CultureInfo.InvariantCulture));

                if (split.Length > 6)
                    readCustomSampleBanks(split[6], ref normalSampleBank, ref addSampleBank, ref sampleVolume);
            }
            else if ((type & HitObjectType.Hold) > 0)
            {
                // Note: Hold is generated by BMS converts

                // Todo: Apparently end time is determined by samples??
                // Shouldn't need implementation until mania

                result = new Hold
                {
                    Position = new Vector2(int.Parse(split[0]), int.Parse(split[1])),
                    NewCombo = combo
                };
            }
            else
                throw new InvalidOperationException($@"Unknown hit object type {type}");

            result.StartTime = Convert.ToDouble(split[2], CultureInfo.InvariantCulture);

            var soundType = (LegacySoundType)int.Parse(split[4]);

            result.Samples.Add(new SampleInfo
            {
                Bank = normalSampleBank,
                Name = SampleInfo.HIT_NORMAL,
                Volume = sampleVolume
            });

            if ((soundType & LegacySoundType.Finish) > 0)
            {
                result.Samples.Add(new SampleInfo
                {
                    Bank = addSampleBank,
                    Name = SampleInfo.HIT_FINISH,
                    Volume = sampleVolume
                });
            }

            if ((soundType & LegacySoundType.Whistle) > 0)
            {
                result.Samples.Add(new SampleInfo
                {
                    Bank = addSampleBank,
                    Name = SampleInfo.HIT_WHISTLE,
                    Volume = sampleVolume
                });
            }

            if ((soundType & LegacySoundType.Clap) > 0)
            {
                result.Samples.Add(new SampleInfo
                {
                    Bank = addSampleBank,
                    Name = SampleInfo.HIT_CLAP,
                    Volume = sampleVolume
                });
            }

            return result;
        }

        private void readCustomSampleBanks(string str, ref string normalSampleBank, ref string addSampleBank, ref int sampleVolume)
        {
            if (string.IsNullOrEmpty(str))
                return;

            string[] split = str.Split(':');

            var bank = (OsuLegacyDecoder.LegacySampleBank)Convert.ToInt32(split[0]);
            var addbank = (OsuLegacyDecoder.LegacySampleBank)Convert.ToInt32(split[1]);

            // Let's not implement this for now, because this doesn't fit nicely into the bank structure
            //string sampleFile = split2.Length > 4 ? split2[4] : string.Empty;

            string stringBank = bank.ToString().ToLower();
            if (stringBank == @"none")
                stringBank = null;
            string stringAddBank = addbank.ToString().ToLower();
            if (stringAddBank == @"none")
                stringAddBank = null;

            normalSampleBank = stringBank;
            addSampleBank = stringAddBank;
            sampleVolume = split.Length > 3 ? int.Parse(split[3]) : 0;
        }

        /// <summary>
        /// Creates a legacy Hit-type hit object.
        /// </summary>
        /// <param name="position">The position of the hit object.</param>
        /// <param name="newCombo">Whether the hit object creates a new combo.</param>
        /// <returns>The hit object.</returns>
        protected abstract HitObject CreateHit(Vector2 position, bool newCombo);

        /// <summary>
        /// Creats a legacy Slider-type hit object.
        /// </summary>
        /// <param name="position">The position of the hit object.</param>
        /// <param name="newCombo">Whether the hit object creates a new combo.</param>
        /// <param name="controlPoints">The slider control points.</param>
        /// <param name="length">The slider length.</param>
        /// <param name="curveType">The slider curve type.</param>
        /// <param name="repeatCount">The slider repeat count.</param>
        /// <returns>The hit object.</returns>
        protected abstract HitObject CreateSlider(Vector2 position, bool newCombo, List<Vector2> controlPoints, double length, CurveType curveType, int repeatCount);

        /// <summary>
        /// Creates a legacy Spinner-type hit object.
        /// </summary>
        /// <param name="position">The position of the hit object.</param>
        /// <param name="endTime">The spinner end time.</param>
        /// <returns>The hit object.</returns>
        protected abstract HitObject CreateSpinner(Vector2 position, double endTime);

        [Flags]
        private enum LegacySoundType
        {
            None = 0,
            Normal = 1,
            Whistle = 2,
            Finish = 4,
            Clap = 8
        }
    }
}