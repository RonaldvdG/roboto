﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Roboto.Helpers;

namespace Roboto.Modules
{
    /// <summary>
    /// General Data to be stored in the plugin XML store.
    /// </summary>
    [XmlType("mod_xyzzy_coredata")]
    [Serializable]
    public class mod_xyzzy_coredata : RobotoModuleDataTemplate
    {
        public DateTime lastDayProcessed = DateTime.MinValue;
        public int backgroundChatsToProcess = 20;
        public List<mod_xyzzy_card> questions = new List<mod_xyzzy_card>();
        public List<mod_xyzzy_card> answers = new List<mod_xyzzy_card>();
        public List<Helpers.cardcast_pack> packs = new List<Helpers.cardcast_pack>();
        //removed - moved into telegramAPI and settings class. 
        //public List<mod_xyzzy_expectedReply> expectedReplies = new List<mod_xyzzy_expectedReply>(); //replies expected by the various chats
        //internal mod_xyzzy_coredata() { }



        public mod_xyzzy_card getQuestionCard(string cardUID)
        {
            foreach (mod_xyzzy_card c in questions)
            {
                if (c.uniqueID == cardUID) { return c; }
            }
            return null;
        }

        public mod_xyzzy_card getAnswerCard(string cardUID)
        {
            foreach (mod_xyzzy_card c in answers)
            {
                if (c.uniqueID == cardUID)
                {
                    return c;
                }
            }
            return null;
        }

        public List<Helpers.cardcast_pack> getPackFilterList()
        {
            return packs;
            /*
            //include "all"
            List<String> packs = new List<string>();
            foreach (mod_xyzzy_card q in questions)
            {
                packs.Add(q.category.Trim());
            }
            foreach (mod_xyzzy_card a in answers)
            {
                packs.Add(a.category.Trim());
            }
            return packs.Distinct().ToList();
            */
        }

        public void startupChecks()
        {
            //check that a pack exists for each card in Q / A
            foreach (mod_xyzzy_card q in questions)
            {
                if (packs.Where(x => x.name == q.category).Count() == 0)
                {
                    log("Creating dummy pack for " + q.category, logging.loglevel.high);
                    packs.Add(new Helpers.cardcast_pack(q.category, "", q.category));
                }
            }
            foreach (mod_xyzzy_card a in answers)
            {
                if (packs.Where(x => x.name == a.category).Count() == 0)
                {
                    log("Creating dummy pack for " + a.category, logging.loglevel.high);
                    packs.Add(new Helpers.cardcast_pack(a.category, "", a.category));
                }
            }

            //remove any dupes. Must keep oldest pack first
            List<Helpers.cardcast_pack> newPackList = new List<cardcast_pack>();
            foreach (cardcast_pack p in packs)
            {
                if (newPackList.Where(x => x.name == p.name).Count() == 0) { newPackList.Add(p); }
            }
            if (packs.Count != newPackList.Count)
            {
                log("Deduped global packlist, was " + packs.Count() + " now " + newPackList.Count(), logging.loglevel.high);
                packs = newPackList;
            }
        }

        /// <summary>
        /// check for any packs that can be (and need to be) synced
        /// </summary>
        public void packSyncCheck()
        {
            //Packs is already a list, but there is a chance that the importCardCast will update / remove it - so re-list it to prevent mutation errors
            foreach (Helpers.cardcast_pack p in packs.ToList())
            {
                if (p.packCode != null && p.packCode != "" && p.nextSync < DateTime.Now)
                {
                    log("Syncing " + p.name);
                    Roboto.Settings.stats.logStat(new statItem("Packs Synced", typeof(mod_xyzzy)));
                    Helpers.cardcast_pack outpack;
                    string response;
                    bool success = importCardCastPack(p.packCode, out outpack, out response);
                    if (!success)
                    {
                        //this is usually set in the method, but it doesnt happen if the call has failed. Set here to prevent repeatedly hammering. 
                        p.nextSync = DateTime.Now.Add(new TimeSpan(5, settings.getRandom(23), 0, 0));
                        log("Failed to sync pack " + p.packCode + " - " + p.description + ". Next sync "+ p.nextSync.ToString("f"), logging.loglevel.warn);
                        
                    }
                    else
                    {
                        log("Synced deck, next sync " + outpack.nextSync.ToString("f"), logging.loglevel.high);
                    }

                }

            }

        }



        /// <summary>
        /// Import a cardcast pack into the xyzzy localdata
        /// </summary>
        /// <param name="packFilter"></param>
        /// <returns>String containing details of the pack and cards added. String will be empty if import failed.</returns>
        public bool importCardCastPack(string packCode, out Helpers.cardcast_pack pack, out string response)
        {
            response = "";
            pack = new Helpers.cardcast_pack();
            bool success = false;
            int nr_qs = 0;
            int nr_as = 0;
            int nr_rep = 0;
            List<Helpers.cardcast_question_card> import_questions = new List<Helpers.cardcast_question_card>();
            List<Helpers.cardcast_answer_card> import_answers = new List<Helpers.cardcast_answer_card>();
            List<mod_xyzzy_chatdata> brokenChats = new List<mod_xyzzy_chatdata>();

            //don't sync again within x days. Add a random duration. 
            //This line does nothing, as we overwrite the pack. 
            //pack.nextSync = DateTime.Now.Add(new TimeSpan(5, settings.getRandom(23), 0, 0));

            try
            {
                log("Attempting to sync/import " + packCode);
                //Call the cardcast API. We should get an array of cards back (but in the wrong format)
                success = Helpers.cardCast.getPackCards(ref packCode, out pack, ref import_questions, ref import_answers);

                if (!success)
                {
                    response = "Failed to import pack from cardcast. Check that the code is valid";
                }
                else
                {
                    //lets just check if the pack already exists? 
                    log("Retrieved " + import_questions.Count() + " questions and " + import_answers.Count() + " answers from Cardcast");
                    string l_packname = pack.name;
                    List<cardcast_pack> matchingPacks = getPackFilterList().Where(x => x.name == l_packname).ToList();

                    if (matchingPacks.Count > 1)  // .Contains(pack.name))
                    {
                        log("Multiple packs found for " + l_packname + " - aborting!", logging.loglevel.critical);
                        response += "/n/r" + "Aborting sync!";

                    }
                    else if (matchingPacks.Count == 1)
                    {
                        cardcast_pack updatePack = matchingPacks[0];

                        //sync the pack.
                        response = "Pack " + pack.name + " (" + packCode + ") exists, syncing cards";
                        log("Pack " + pack.name + "(" + packCode + ") exists, syncing cards", logging.loglevel.normal);

                        //remove any cached questions that no longer exist. Add them to a list first to allow us to loop;
                        List<mod_xyzzy_card> remove_cards = new List<mod_xyzzy_card>();
                        //ignore any cards that already exist in the cache. Adde them to a list first to allow us to loop;
                        List<mod_xyzzy_card> exist_cards = new List<mod_xyzzy_card>();
                        foreach (mod_xyzzy_card q in questions.Where(x => x.category == l_packname))
                        {
                            //find existing cards which don't exist in our import pack
                            if (import_questions.Where(y => Helpers.common.cleanseText(y.question) == Helpers.common.cleanseText(q.text)).Count() == 0)
                            {
                                remove_cards.Add(q);
                            }
                            //if they do already exist, remove them from the import list (because they exist!)
                            else
                            {
                                exist_cards.Add(q);
                            }
                        }
                        //now remove them from the localdata
                        foreach (mod_xyzzy_card q in remove_cards)
                        {
                            questions.Remove(q);
                            //remove any cached questions
                            foreach(chat c in Roboto.Settings.chatData)
                            {
                                
                                mod_xyzzy_chatdata chatData = (mod_xyzzy_chatdata) c.getPluginData(typeof(mod_xyzzy_chatdata));
                                if (chatData != null)
                                {
                                    chatData.remainingQuestions.RemoveAll(x => x == q.uniqueID);
                                    //if we remove the current question, invalidate the chat. Will reask a question once the rest of the import is done. 
                                    if (chatData.currentQuestion == q.uniqueID)
                                    {
                                        log("The current question " + chatData.currentQuestion + " for chat " + c.chatID + " has been removed!");
                                        if (!brokenChats.Contains(chatData)) { brokenChats.Add(chatData); }
                                    }
                                }
                            }
                        }
                        //or from the import list 
                        foreach (mod_xyzzy_card q in exist_cards)
                        {
                            //update the local text if it was a match-
                            cardcast_question_card match = null;
                            try
                            {
                                 match = import_questions.Where(y => Helpers.common.cleanseText(y.question) == Helpers.common.cleanseText(q.text)).ToList()[0];
                            }
                            catch (Exception e)
                            {
                                log("Error finding cleansed version of q card - " + e.Message, logging.loglevel.critical);
                            }

                            if (match != null && q.text != match.question)
                            {
                                try
                                {
                                    log("Question text updated from " + q.text + " to " + match.question);
                                    q.text = match.question;
                                    q.nrAnswers = match.nrAnswers;
                                    nr_rep++;
                                }
                                catch (Exception e)
                                {
                                    log("Error updating question text on qcard - " + e.Message, logging.loglevel.critical);
                                }
                            }
                            try
                            {
                                int removed = import_questions.RemoveAll(x => x.question == q.text); //swallow this. 
                            }
                            catch (Exception e)
                            {
                                log("Error removing qcard from importlist - " + e.Message, logging.loglevel.critical);
                            }
                        }
                        //add the rest to the localData
                        foreach (Helpers.cardcast_question_card q in import_questions)
                        {
                            mod_xyzzy_card x_question = new mod_xyzzy_card(q.question, pack.name, q.nrAnswers);
                            questions.Add(x_question);
                        }
                        response += "\n\r" + "Qs: Removed " + remove_cards.Count() + " from local. Skipped " + exist_cards.Count() + " as already exist. Updated " + nr_rep + ". Added " + import_questions.Count() + " new / replacement cards";


                        //do the same for the answer cards
                        nr_rep = 0;
                        remove_cards.Clear();
                        exist_cards.Clear();
                        foreach (mod_xyzzy_card a in answers.Where(x => x.category == l_packname))
                        {
                            //find existing cards which don't exist in our import pack
                            if (import_answers.Where(y => Helpers.common.cleanseText(y.answer) == Helpers.common.cleanseText(a.text)).Count() == 0)
                            {
                                remove_cards.Add(a);
                            }
                            //if they do already exist, remove them from the import list (because they exist!)
                            else
                            {
                                exist_cards.Add(a);
                            }
                        }
                        //now remove them from the localdata
                        foreach (mod_xyzzy_card a in remove_cards) { answers.Remove(a); }
                        //or from the import list
                        foreach (mod_xyzzy_card a in exist_cards)
                        {
                            //update the local text if it was a match-ish
                            List<cardcast_answer_card> amatches = import_answers.Where(y => Helpers.common.cleanseText(y.answer) == Helpers.common.cleanseText(a.text)).ToList();
                            if (amatches.Count > 0)
                            {
                                cardcast_answer_card matcha = amatches[0];
                                if (a.text != matcha.answer)
                                {
                                    log("Answer text updated from " + a.text + " to " + matcha.answer);
                                    a.text = matcha.answer;
                                    nr_rep++;
                                }
                            }
                            else
                            {
                                log("Couldnt find card to update! " + a.text, logging.loglevel.high);
                            }

                            int removed = import_answers.RemoveAll(x => x.answer == a.text);
                        }
                        //add the rest to the localData
                        foreach (Helpers.cardcast_answer_card a in import_answers)
                        {
                            mod_xyzzy_card x_answer = new mod_xyzzy_card(a.answer, pack.name);
                            answers.Add(x_answer);
                        }

                        
                        response += "\n\r" + "As: Removed " + remove_cards.Count() + " from local. Skipped " + exist_cards.Count() + " as already exist. Updated " + nr_rep + ". Added " + import_answers.Count() + " new / replacement cards";

                        updatePack.description = pack.description;
                        //don't sync again within x days. Add a random duration. 
                        updatePack.nextSync = DateTime.Now.Add(new TimeSpan(5, settings.getRandom(23), 0, 0));
                        response += "\n\r" + "Next sync " + updatePack.nextSync.ToString("f") + ".";
                        //pack.description = outpack.description;

                        Roboto.Settings.stats.logStat(new statItem("Packs Synced", typeof(mod_xyzzy)));
                        

                        success = true;
                    }
                    else
                    {
                        response += "Importing fresh pack " + pack.packCode + " - " + pack.name + " - " + pack.description;
                        foreach (Helpers.cardcast_question_card q in import_questions)
                        {
                            mod_xyzzy_card x_question = new mod_xyzzy_card(q.question, pack.name, q.nrAnswers);
                            questions.Add(x_question);
                            nr_qs++;
                        }
                        foreach (Helpers.cardcast_answer_card a in import_answers)
                        {
                            mod_xyzzy_card x_answer = new mod_xyzzy_card(a.answer, pack.name);
                            answers.Add(x_answer);
                            nr_as++;
                        }
                        
                        response += "\n\r" + "Next sync " + pack.nextSync.ToString("f") + ".";

                        response += "\n\r" + "Added " + nr_qs.ToString() + " questions and " + nr_as.ToString() + " answers.";
                        packs.Add(pack);
                        response += "\n\r" + "Added " + pack.name + " to filter list.";
                    }
                    

                    
                    
                }
            }
            catch (Exception e)
            {
                log("Failed to import pack " + e.ToString(), logging.loglevel.critical);
                success = false;
            }

            foreach (mod_xyzzy_chatdata c in brokenChats)
            {
                c.askQuestion(false);
            }

            log(response, logging.loglevel.normal);

            return success;

        }

        public void removeDupeCards()
        {
            //loop through each pack. Pack filter should be up-to date even if this is called from the startup-checks.
            foreach (cardcast_pack pack in packs)
            {
                //add each card to one of these lists depending on whether it has been seen or not. Remove the removal ones afterwards.
                List<mod_xyzzy_card> validQCards = new List<mod_xyzzy_card>();
                List<mod_xyzzy_card> removeQCards = new List<mod_xyzzy_card>();
                foreach (mod_xyzzy_card c in questions.Where(y => y.category == pack.name) )
                {
                    //is there a matching card already?
                    List<mod_xyzzy_card> matchList = validQCards.Where(x => (x.category == pack.name && Helpers.common.cleanseText(x.text) == Helpers.common.cleanseText(c.text))).ToList();
                    if (matchList.Count() > 1)
                    {
                        removeQCards.Add(c);
                        //updating any references in active games. 
                        replaceCardReferences(c, matchList[0], "Q");
                    }
                    else
                    {
                        validQCards.Add(c);
                    }
                }
                //remove any flagged cards
                foreach (mod_xyzzy_card c in removeQCards) { questions.Remove(c); }

                //Repeat for answers
                List<mod_xyzzy_card> validACards = new List<mod_xyzzy_card>();
                List<mod_xyzzy_card> removeACards = new List<mod_xyzzy_card>();
                foreach (mod_xyzzy_card c in answers.Where(y => y.category == pack.name))
                {
                    //is there a matching card already?
                    List<mod_xyzzy_card> matchList = validACards.Where(x => (x.category == pack.name && Helpers.common.cleanseText(x.text) == Helpers.common.cleanseText(c.text))).ToList();
                    if (matchList.Count() > 1)
                    {
                        removeACards.Add(c);
                        //updating any references in active games. 
                        replaceCardReferences(c, matchList[0], "A");
                    }
                    else
                    {
                        validACards.Add(c);
                    }
                }
                //remove any flagged cards
                int total = removeQCards.Count() + removeACards.Count();
                foreach (mod_xyzzy_card c in removeACards) { answers.Remove(c); }
                log("Removed " + removeQCards.Count() + " / " + removeACards.Count() 
                    + " duplicate q/a from " + pack.name 
                    + " new totals are " + questions.Where(y => y.category == pack.name).Count() 
                    + " / " + answers.Where(y => y.category == pack.name).Count() + " q/a."
                    , total > 0? logging.loglevel.warn:logging.loglevel.verbose);


            }


        }

        private void replaceCardReferences(mod_xyzzy_card old, mod_xyzzy_card newcard, string cardType)
        {
            foreach (chat c in Roboto.Settings.chatData)
            {
                mod_xyzzy_chatdata chatdata = (mod_xyzzy_chatdata)c.getPluginData(typeof(mod_xyzzy_chatdata));
                if (chatdata != null)
                {
                    chatdata.replaceCard(old, newcard, cardType);
                }
            }
        }
    }
}
