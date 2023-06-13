using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public interface IPlayer
{
    Card DrawCard();
    void FinishTurn(Card playedCard);
    void StartNewGame();
    string GetIndex();
    List<Card> GetDeck();
    void SetDeck(List<Card> deck);
    void SetDeck(string deckRepresentation);
}
