using System.Collections.Generic;

namespace Aruun.Core.Language
{
    /// <summary>
    /// How well a character knows a word. Learning Avarra is the game's core progression:
    /// you see a symbol in a ruin, see it again elsewhere, form a guess about its meaning,
    /// and only once you truly know it can you speak it.
    /// </summary>
    public enum Familiarity { Unseen = 0, Seen = 1, Guessed = 2, Known = 3 }

    /// <summary>One character's knowledge of the language. Saved with the character, not the world.</summary>
    public sealed class WordKnowledge
    {
        /// <summary>Sightings in different places needed before a word can be guessed at.</summary>
        public const int SightingsToGuess = 2;

        readonly Dictionary<string, Familiarity> _state = new Dictionary<string, Familiarity>();
        readonly Dictionary<string, HashSet<string>> _sightings = new Dictionary<string, HashSet<string>>();

        public Familiarity Of(string word)
        {
            Familiarity f;
            return _state.TryGetValue(word, out f) ? f : Familiarity.Unseen;
        }

        public bool Knows(string word) { return Of(word) == Familiarity.Known; }

        /// <summary>
        /// The character read an inscription at a place (any stable id: a ruin, a stone).
        /// Seeing it again in the same place teaches nothing new. Returns the new familiarity.
        /// </summary>
        public Familiarity See(string word, string place)
        {
            HashSet<string> places;
            if (!_sightings.TryGetValue(word, out places)) _sightings[word] = places = new HashSet<string>();
            places.Add(place);
            Familiarity f = Of(word);
            if (f == Familiarity.Unseen) f = Familiarity.Seen;
            if (f == Familiarity.Seen && places.Count >= SightingsToGuess) f = Familiarity.Guessed;
            _state[word] = f;
            return f;
        }

        public int Sightings(string word)
        {
            HashSet<string> places;
            return _sightings.TryGetValue(word, out places) ? places.Count : 0;
        }

        /// <summary>
        /// Confirms a word's meaning: the character spoke it at a mechanism that answered,
        /// or a teacher explained it. Only a guessed (or already known) word can be confirmed.
        /// </summary>
        public bool Confirm(string word)
        {
            Familiarity f = Of(word);
            if (f < Familiarity.Guessed) return false;
            _state[word] = Familiarity.Known;
            return true;
        }

        /// <summary>Learn outright, for teachers, quest rewards and dev tools.</summary>
        public void Teach(string word) { _state[word] = Familiarity.Known; }

        public IEnumerable<KeyValuePair<string, Familiarity>> All { get { return _state; } }
    }
}
