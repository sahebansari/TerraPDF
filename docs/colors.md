# Colors

TerraPDF ships a full **Material Design**-inspired colour palette as static string
constants in the `TerraPDF.Helpers.Color` class. All values are CSS hex strings
compatible with every API that accepts a colour (`FontColor`, `Background`,
`Border`, `LineHorizontal`, etc.).

---

## Special Constants

```csharp
Color.White        // "#FFFFFF"
Color.Black        // "#000000"
Color.Transparent  // "#00000000"
```

---

## Full Palette

Each colour family exposes shades from `Lighten5` (near-white) through `Medium`
to `Darken4` (near-black). Not all families have every shade.

### Red
| Constant | Hex |
|----------|-----|
| `Color.Red.Lighten5` | `#FFEBEE` |
| `Color.Red.Lighten4` | `#FFCDD2` |
| `Color.Red.Lighten3` | `#EF9A9A` |
| `Color.Red.Lighten2` | `#E57373` |
| `Color.Red.Lighten1` | `#EF5350` |
| `Color.Red.Medium`   | `#F44336` |
| `Color.Red.Darken1`  | `#E53935` |
| `Color.Red.Darken2`  | `#D32F2F` |
| `Color.Red.Darken3`  | `#C62828` |
| `Color.Red.Darken4`  | `#B71C1C` |

### Pink
| Constant | Hex |
|----------|-----|
| `Color.Pink.Medium`   | `#E91E63` |
| `Color.Pink.Darken2`  | `#C2185B` |

### Purple
| Constant | Hex |
|----------|-----|
| `Color.Purple.Medium`   | `#9C27B0` |
| `Color.Purple.Darken2`  | `#7B1FA2` |

### Deep Purple
| Constant | Hex |
|----------|-----|
| `Color.DeepPurple.Medium`   | `#673AB7` |
| `Color.DeepPurple.Darken2`  | `#512DA8` |

### Indigo
| Constant | Hex |
|----------|-----|
| `Color.Indigo.Lighten5` | `#E8EAF6` |
| `Color.Indigo.Medium`   | `#3F51B5` |
| `Color.Indigo.Darken2`  | `#283593` |

### Blue
| Constant | Hex |
|----------|-----|
| `Color.Blue.Lighten5` | `#E3F2FD` |
| `Color.Blue.Lighten4` | `#BBDEFB` |
| `Color.Blue.Lighten3` | `#90CAF9` |
| `Color.Blue.Lighten2` | `#64B5F6` |
| `Color.Blue.Lighten1` | `#42A5F5` |
| `Color.Blue.Medium`   | `#2196F3` |
| `Color.Blue.Darken1`  | `#1E88E5` |
| `Color.Blue.Darken2`  | `#1565C0` |

### Teal
| Constant | Hex |
|----------|-----|
| `Color.Teal.Medium`   | `#009688` |
| `Color.Teal.Darken2`  | `#00796B` |

### Green
| Constant | Hex |
|----------|-----|
| `Color.Green.Lighten5` | `#E8F5E9` |
| `Color.Green.Lighten4` | `#C8E6C9` |
| `Color.Green.Lighten3` | `#A5D6A7` |
| `Color.Green.Lighten2` | `#81C784` |
| `Color.Green.Lighten1` | `#66BB6A` |
| `Color.Green.Medium`   | `#4CAF50` |
| `Color.Green.Darken1`  | `#43A047` |
| `Color.Green.Darken2`  | `#388E3C` |
| `Color.Green.Darken3`  | `#2E7D32` |
| `Color.Green.Darken4`  | `#1B5E20` |

### Light Green
| Constant | Hex |
|----------|-----|
| `Color.LightGreen.Medium`   | `#8BC34A` |
| `Color.LightGreen.Darken2`  | `#689F38` |

### Lime
| Constant | Hex |
|----------|-----|
| `Color.Lime.Medium`   | `#CDDC39` |
| `Color.Lime.Darken2`  | `#AFB42B` |

### Yellow
| Constant | Hex |
|----------|-----|
| `Color.Yellow.Medium`   | `#FFEB3B` |
| `Color.Yellow.Darken2`  | `#F9A825` |

### Amber
| Constant | Hex |
|----------|-----|
| `Color.Amber.Medium`   | `#FFC107` |
| `Color.Amber.Darken2`  | `#FF8F00` |

### Orange
| Constant | Hex |
|----------|-----|
| `Color.Orange.Medium`   | `#FF9800` |
| `Color.Orange.Darken2`  | `#E65100` |

### Deep Orange
| Constant | Hex |
|----------|-----|
| `Color.DeepOrange.Medium`   | `#FF5722` |
| `Color.DeepOrange.Darken2`  | `#BF360C` |

### Brown
| Constant | Hex |
|----------|-----|
| `Color.Brown.Lighten5` | `#EFEBE9` |
| `Color.Brown.Medium`   | `#795548` |
| `Color.Brown.Darken2`  | `#4E342E` |

### Grey
| Constant | Hex |
|----------|-----|
| `Color.Grey.Lighten5` | `#FAFAFA` |
| `Color.Grey.Lighten4` | `#F5F5F5` |
| `Color.Grey.Lighten3` | `#EEEEEE` |
| `Color.Grey.Lighten2` | `#E0E0E0` |
| `Color.Grey.Lighten1` | `#BDBDBD` |
| `Color.Grey.Medium`   | `#9E9E9E` |
| `Color.Grey.Darken1`  | `#757575` |
| `Color.Grey.Darken2`  | `#616161` |
| `Color.Grey.Darken3`  | `#424242` |
| `Color.Grey.Darken4`  | `#212121` |

### Blue Grey
| Constant | Hex |
|----------|-----|
| `Color.BlueGrey.Lighten5` | `#ECEFF1` |
| `Color.BlueGrey.Lighten4` | `#CFD8DC` |
| `Color.BlueGrey.Medium`   | `#607D8B` |
| `Color.BlueGrey.Darken2`  | `#37474F` |
| `Color.BlueGrey.Darken4`  | `#263238` |

---

## Using Raw Hex Strings

Any method that accepts a colour string also accepts a plain hex literal:

```csharp
container.Background("#FFF8E1");
container.FontColor("#333333");
container.Border(1, "#DDDDDD");
```
