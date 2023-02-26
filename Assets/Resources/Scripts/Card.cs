using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CardColor { Red, Green, Yellow, Blue };
public class Card : MonoBehaviour
{
    /*
        1-9 are regular
        10 is skip
        11 is reverse
        12 is draw 2
        13 is wild
        14 is wild draw 4
    */

    readonly private int number;
    private CardColor color;

    public Card(int number, CardColor color)
    { 
        this.number = number;
        this.color = color;
    }

    public int GetNumber() { return this.number; }
    public CardColor GetColor() { return this.color; }
    public void ChangeColor(CardColor newColor)
    { 
        //mutator that changes the color of a wild card to make the color noticeable
        if (GetNumber() == 13 || GetNumber() == 14)
        {
            this.color = newColor;
        }
        else
        {
            Debug.Log("Error: card isn't wild, cannot change color");
        }
    }
}
