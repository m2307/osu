﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.OsuDifficulty.Preprocessing;
using osu.Game.Rulesets.Osu.OsuDifficulty.Utils;

namespace osu.Game.Rulesets.Osu.OsuDifficulty.Skills
{
    public abstract class Skill
    {
        /// <summary>
        /// Strain values are multiplied by this number for the given skill. Used to balance the value of different skills between each other.
        /// </summary>
        protected abstract double SkillMultiplier { get; }

        /// <summary>
        /// Determines how quickly strain decays for the given skill.
        /// For example a value of 0.15 indicates that strain decays to 15% of it's original value in one second.
        /// </summary>
        protected abstract double StrainDecayBase { get; }

        /// <summary>
        /// The note that will be processed.
        /// </summary>
        protected OsuDifficultyHitObject Current;

        /// <summary>
        /// Notes that were processed previously. They can affect the strain value of the current note.
        /// </summary>
        protected History<OsuDifficultyHitObject> Previous = new History<OsuDifficultyHitObject>(2); // Contained objects not used yet

        private double currentStrain = 1; // We keep track of the strain level at all times throughout the beatmap.
        private double currentSectionPeak = 1; // We also keep track of the peak strain level in the current section.
        private readonly List<double> strainPeaks = new List<double>();

        /// <summary>
        /// Process a HitObject and update current strain values accordingly.
        /// </summary>
        public void Process(OsuDifficultyHitObject h)
        {
            Current = h;

            currentStrain *= strainDecay(Current.DeltaTime);
            if (!(Current.BaseObject is Spinner))
                currentStrain += StrainValue() * SkillMultiplier;

            currentSectionPeak = Math.Max(currentStrain, currentSectionPeak);

            Previous.Push(Current);
        }

        /// <summary>
        /// Saves the current peak strain level to the list of strain peaks, which will be used to calculate an overall difficulty.
        /// </summary>
        public void SaveCurrentPeak()
        {
            if (Previous.Count > 0)
                strainPeaks.Add(currentSectionPeak);
        }

        /// <summary>
        /// Sets the initial strain level for a new section.
        /// </summary>
        /// <param name="offset">The beginning of the new section in milliseconds</param>
        public void StartNewSectionFrom(double offset)
        {
            // The maximum strain of the new section is not zero by default, strain decays as usual regardless of section boundaries.
            // This means we need to capture the strain level at the beginning of the new section, and use that as the initial peak level.
            if (Previous.Count > 0)
                currentSectionPeak = currentStrain * strainDecay(offset - Previous[0].BaseObject.StartTime);
        }

        /// <summary>
        /// Returns the calculated difficulty value representing all currently processed HitObjects.
        /// </summary>
        public double DifficultyValue()
        {
            strainPeaks.Sort((a, b) => b.CompareTo(a)); // Sort from highest to lowest strain.

            double difficulty = 0;
            double weight = 1;

            // Difficulty is the weighted sum of the highest strains from every section.
            foreach (double strain in strainPeaks)
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            return difficulty;
        }

        /// <summary>
        /// Calculates the strain value of the current note. This value is affected by previous notes.
        /// </summary>
        protected abstract double StrainValue();

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}
