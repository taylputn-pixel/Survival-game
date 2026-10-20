using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The household journal: recipe cards are taken into it, books stay and
    // give it a page at a time, and only what's been read shows.
    public class JournalTests
    {
        PlayerHands hands;
        Discoveries discoveries;
        JournalView journal;
        NoteReader reader;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            discoveries = Homestead.Find<Discoveries>();
            journal = Homestead.Find<JournalView>();
            reader = Homestead.Find<NoteReader>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static NoteData NoteOf(Readable source) =>
            (NoteData)typeof(Readable).GetField("note", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(source);

        static Readable Source(string noteName)
        {
            foreach (var r in Object.FindObjectsByType<Readable>(FindObjectsInactive.Include))
            {
                var note = NoteOf(r);
                if (note != null && note.name == noteName) return r;
            }
            Assert.Fail("Nothing to read called " + noteName);
            return null;
        }

        List<JournalView.Entry> Section(int tab)
        {
            var entries = new List<JournalView.Entry>();
            journal.Gather(tab, entries);
            return entries;
        }

        IEnumerator CloseReader()
        {
            while (reader.IsOpen)
            {
                typeof(NoteReader).GetMethod("Close", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(reader, null);
                yield return null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARecipeCardIsTakenIntoTheJournal()
        {
            var card = Source("Note_SalsaCard");
            Assert.IsTrue(card.gameObject.activeInHierarchy);
            Assert.AreEqual(0, Section(0).Count, "The journal should start with no recipes.");
            Assert.AreEqual("Read and keep", card.PromptVerb(hands));

            card.Interact(hands);
            yield return null;
            Assert.IsFalse(card.gameObject.activeSelf, "The card should be gone from the table once read.");
            var recipes = Section(0);
            Assert.AreEqual(1, recipes.Count);
            Assert.AreEqual("Salsa", recipes[0].title);
            yield return CloseReader();
        }

        [UnityTest]
        public IEnumerator BooksStayAndGiveOnlyThePagesRead()
        {
            var book = Source("Book_KeepingHens");
            book.Interact(hands);
            yield return null;
            Assert.IsTrue(book.gameObject.activeSelf, "A book stays where it is.");
            Assert.AreEqual(1, Section(1).Count, "Only the first page has been read.");

            // Turn one page.
            typeof(NoteReader).GetMethod("TurnTo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(reader, new object[] { 1 });
            var howTo = Section(1);
            Assert.AreEqual(2, howTo.Count);
            Assert.AreEqual("Feed and water", howTo[1].title, "A page's heading line is its title.");
            StringAssert.StartsWith("A scoop", howTo[1].body);
            Assert.AreEqual("From Keeping Hens", howTo[1].source);
            yield return CloseReader();
        }

        [UnityTest]
        public IEnumerator TheJournalIsSavedAndALoadedSaveBringsTheCardBack()
        {
            string fresh = discoveries.Capture();
            var card = Source("Note_PotatoSoupCard");
            card.Interact(hands);
            discoveries.RecordPage(NoteOf(Source("Book_MeatThatKeeps")), 2);
            yield return CloseReader();

            string saved = discoveries.Capture();
            discoveries.Restore(fresh);
            yield return null;
            Assert.IsTrue(card.gameObject.activeSelf, "Loading a save from before it was read puts the card back.");
            Assert.AreEqual(0, Section(1).Count);

            discoveries.Restore(saved);
            yield return null;
            Assert.IsFalse(card.gameObject.activeSelf);
            Assert.AreEqual(1, Section(0).Count);
            var howTo = Section(1);
            Assert.AreEqual(1, howTo.Count);
            Assert.AreEqual("The salt box", howTo[0].title);
        }

        [UnityTest]
        public IEnumerator TheJournalOpensAndClosesAndTurnsSections()
        {
            Source("Note_SalsaCard").Interact(hands);
            yield return CloseReader();
            yield return null;

            journal.Open();
            yield return null;
            Assert.IsTrue(journal.IsOpen);
            Assert.IsFalse(hands.Controller.InputEnabled, "Walking stops while the journal is open.");
            Assert.AreEqual(0, journal.Tab);
            Assert.AreEqual(1, journal.Entries.Count);

            journal.ShowTab(1);
            Assert.AreEqual(0, journal.Entries.Count, "Nothing in How-to yet: no hints, just blank pages.");

            journal.Close();
            yield return null;
            yield return null;
            Assert.IsFalse(journal.IsOpen);
            Assert.IsTrue(hands.Controller.InputEnabled);
        }
    }
}
