using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum CardColor { Red, Green, Yellow, Blue , Wild};
public class Card : MonoBehaviour
{
    /*
        1-10 are regular (1, 2, 3... 0)
        11 is draw 2
        12 is skip
        13 is reverse
        14 is wild
        15 is wild draw 4
    */

    [SerializeField] int number;
    [SerializeField] CardColor color;
    private SpriteRenderer cardSprite;

    private static Sprite[] cardSpriteSheet;
    void Start()
    {
        cardSpriteSheet = Resources.LoadAll<Sprite>("Graphics/Cards");
        cardSprite = GetComponent<SpriteRenderer>();
        int spritePathNum = 999;
        switch (color)
        {
            case CardColor.Red:
                spritePathNum = 24 + number; break; 
            case CardColor.Green:
                spritePathNum = 50 + number; break; 
            case CardColor.Yellow:
                spritePathNum = 11 + number; break;
            case CardColor.Blue:
                spritePathNum = 37 + number; break;
            case CardColor.Wild:
                switch (number)
                {
                    case 14:
                        spritePathNum = 1; break;
                    case 15:
                        spritePathNum = 6; break;
                    default:
                        spritePathNum = 11;
                        Debug.Log("Error: card num is not valid. Defaulting to White Wild.");
                        break;
                }
                break;
        }
        //string spritePath = "Graphics/Cards_" + spritePathNum.ToString();
        //cardSprite.sprite = Resources.Load<Sprite>(spritePath);
        cardSprite.sprite = cardSpriteSheet[spritePathNum];

        /*
        1 - wild
        6 - wild draw 4
        12-24 - yellows - numbers; draw 2; skip; reverse
        25-37 - reds - numbers; draw 2; skip; reverse
        38-50 - blues - numbers; draw 2; skip; reverse
        51-63 - greens - numbers; draw 2; skip; reverse
         */
    }

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
