#!/usr/bin/env python3
"""Generates Views/CalculatorView.axaml (keypads are repetitive, so they are generated)."""
import sys
from xml.sax.saxutils import escape, quoteattr

def attr(v):
    return quoteattr(v)

def B(content, cmd=None, dyn=None, cls="", enabled=None, cmdprop="PressCommand", content_bind=None, sel=None, extra="", icon=None):
    if icon:
        cls = (cls + " ico").strip()
    a = ['<Button Classes="key%s"' % ((" " + cls) if cls else "")]
    if icon:
        a.append('Content="{x:Static ic:Icons.%s}"' % icon)
    elif content_bind:
        a.append('Content="{Binding %s}"' % content_bind)
    else:
        a.append("Content=%s" % attr(content))
    a.append('Command="{Binding %s}"' % cmdprop)
    if cmd:
        a.append('CommandParameter="{x:Static e:Command.%s}"' % cmd)
    elif dyn:
        a.append('CommandParameter="{Binding %s}"' % dyn)
    if enabled:
        a.append('IsEnabled="{Binding %s}"' % enabled)
    if sel:
        a.append('Classes.sel="{Binding %s}"' % sel)
    if extra:
        a.append(extra)
    return "      " + " ".join(a) + "/>"

def block(rows, indent="    "):
    return "\n".join(rows)

# ---------------- main pad (4 columns x 6 rows)
main = [
    B(None, "CommandPERCENT", enabled="IsStandardOrScientific", icon="Percent"),
    B("CE", "CommandCENTR"),
    B("C", "CommandCLEAR"),
    B(None, "CommandBACK", icon="Backspace"),
    B("1/x", "CommandREC", enabled="IsStandardOrScientific"),
    B(None, dyn="SquareCmd", content_bind="SquareLabel", enabled="IsStandardOrScientific"),
    B(None, dyn="RootCmd", content_bind="RootLabel", enabled="IsStandardOrScientific"),
    B(None, "CommandDIV", icon="Divide"),
    B("7", "Command7", cls="digit", enabled="IsRadixAtLeastOctal"),
    B("8", "Command8", cls="digit", enabled="IsRadixAtLeastDecimal"),
    B("9", "Command9", cls="digit", enabled="IsRadixAtLeastDecimal"),
    B(None, "CommandMUL", icon="X"),
    B("4", "Command4", cls="digit", enabled="IsRadixAtLeastOctal"),
    B("5", "Command5", cls="digit", enabled="IsRadixAtLeastOctal"),
    B("6", "Command6", cls="digit", enabled="IsRadixAtLeastOctal"),
    B(None, "CommandSUB", icon="Minus"),
    B("1", "Command1", cls="digit"),
    B("2", "Command2", cls="digit", enabled="IsRadixAtLeastOctal"),
    B("3", "Command3", cls="digit", enabled="IsRadixAtLeastOctal"),
    B(None, "CommandADD", icon="Plus"),
    B(None, "CommandSIGN", enabled="IsDecRadix", icon="PlusMinus"),
    B("0", "Command0", cls="digit"),
    B(".", "CommandPNT", cls="digit", enabled="IsDecimalPointEnabled"),
    B(None, "CommandEQU", cls="accent", icon="EqualsSign"),
]

# ---------------- scientific panel (5 columns x 6 rows)
S = "small"
sci = [
    B("2nd", cls=S, cmdprop="ToggleSecondCommand", sel="Is2nd"),
    B("π", "CommandPI", cls=S),
    B("e", "CommandEuler", cls=S),
    B(None, cls=S, cmdprop="CycleAngleCommand", content_bind="AngleLabel"),
    B("F-E", cls=S, cmdprop="ToggleFECommand", sel="IsFE"),

    B(None, dyn="PowCmd", cls=S, content_bind="PowLabel"),
    B(None, dyn="TenPowCmd", cls=S, content_bind="TenPowLabel"),
    B(None, dyn="LogCmd", cls=S, content_bind="LogLabel"),
    B(None, dyn="LnCmd", cls=S, content_bind="LnLabel"),
    B("mod", "CommandMOD", cls=S),

    B(None, dyn="SinCmd", cls=S, content_bind="SinLabel"),
    B(None, dyn="CosCmd", cls=S, content_bind="CosLabel"),
    B(None, dyn="TanCmd", cls=S, content_bind="TanLabel"),
    B("hyp", cls=S, cmdprop="ToggleHypCommand", sel="IsHyp"),
    B("rand", "CommandRand", cls=S),

    B(None, dyn="SecCmd", cls=S, content_bind="SecLabel"),
    B(None, dyn="CscCmd", cls=S, content_bind="CscLabel"),
    B(None, dyn="CotCmd", cls=S, content_bind="CotLabel"),
    B("dms", "CommandDMS", cls=S),
    B("deg", "CommandDegrees", cls=S),

    B("|x|", "CommandAbs", cls=S),
    B("⌊x⌋", "CommandFloor", cls=S),
    B("⌈x⌉", "CommandCeil", cls=S),
    B("exp", "CommandEXP", cls=S),
    B("n!", "CommandFAC", cls=S),

    B(None, "CommandOPENP", cls=S, content_bind="OpenParenText"),
    B(")", "CommandCLOSEP", cls=S),
]

# ---------------- programmer panel (5 columns x 5 rows)
prog = [
    B("<<", "CommandLSHF", cls=S),
    B(">>", "CommandRSHF", cls=S),
    B(">>>", "CommandRSHFL", cls=S),
    B("RoL", "CommandROL", cls=S),
    B("RoR", "CommandROR", cls=S),

    B("RoL c", "CommandROLC", cls=S),
    B("RoR c", "CommandRORC", cls=S),
    B("NOT", "CommandNot", cls=S),
    B("AND", "CommandAnd", cls=S),
    B("NAND", "CommandNand", cls=S),

    B("OR", "CommandOR", cls=S),
    B("NOR", "CommandNor", cls=S),
    B("XOR", "CommandXor", cls=S),
    B("mod", "CommandMOD", cls=S),
    B(None, cls=S, cmdprop="CycleWordSizeCommand", content_bind="WordSizeLabel"),

    B("A", "CommandA", cls="small digit", enabled="IsHexRadix"),
    B("B", "CommandB", cls="small digit", enabled="IsHexRadix"),
    B("C", "CommandC", cls="small digit", enabled="IsHexRadix"),
    B("D", "CommandD", cls="small digit", enabled="IsHexRadix"),
    B(None, "CommandOPENP", cls=S, content_bind="OpenParenText"),

    B("E", "CommandE", cls="small digit", enabled="IsHexRadix"),
    B("F", "CommandF", cls="small digit", enabled="IsHexRadix"),
    B(")", "CommandCLOSEP", cls=S),
    B("bits", cls=S, cmdprop="ToggleBitFlipCommand"),
]

def radix_row(label, text_prop, sel_prop, radix):
    return f'''        <Button Classes="flat" HorizontalAlignment="Stretch" HorizontalContentAlignment="Stretch" Padding="8,1"
                Classes.sel="{{Binding {sel_prop}}}"
                Command="{{Binding SetRadixCommand}}" CommandParameter="{{x:Static e:RadixType.{radix}}}">
          <Grid ColumnDefinitions="48,*">
            <TextBlock Text="{label}" Classes="sub" FontWeight="SemiBold" VerticalAlignment="Center"/>
            <TextBlock Grid.Column="1" Text="{{Binding {text_prop}}}" HorizontalAlignment="Right" FontSize="15" TextTrimming="CharacterEllipsis"/>
          </Grid>
        </Button>'''

xaml = f'''<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:e="using:Calculator.Engine"
             xmlns:loc="using:CalcApp.Localization"
             xmlns:ic="using:CalcApp"
             x:Class="CalcApp.Views.CalculatorView">
  <UserControl.Styles>
    <Style Selector="TextBlock.disp">
      <Setter Property="Foreground" Value="{{DynamicResource Fg}}"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="FontSize" Value="54"/>
    </Style>
    <Style Selector="TextBlock.disp.err">
      <Setter Property="Foreground" Value="{{DynamicResource ErrorFg}}"/>
    </Style>
  </UserControl.Styles>

  <Grid x:Name="Root" RowDefinitions="Auto,*" ColumnDefinitions="*" Margin="8,0,8,8">

    <!-- Display -->
    <Grid x:Name="DisplayPanel" Grid.Row="0" RowDefinitions="Auto,Auto,Auto,Auto" Margin="4,0,4,6">
      <Grid Grid.Row="0" ColumnDefinitions="Auto,*,Auto">
        <TextBlock Grid.Column="0" Classes="sub" Text="{{Binding OpenParenText}}" IsVisible="{{Binding HasOpenParen}}"
                   VerticalAlignment="Center" Margin="0,0,8,0"/>
        <TextBlock Grid.Column="1" Classes="sub" Text="{{Binding ExpressionText}}" FontSize="14" MinHeight="22"
                   HorizontalAlignment="Right" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
        <Button Grid.Column="2" Classes="flat ico" Content="{{x:Static ic:Icons.ClockCounterClockwise}}" IsVisible="{{Binding HasHistory}}"
                ToolTip.Tip="{{loc:T Key=History}}"
                Command="{{Binding ToggleHistoryCommand}}" Classes.sel="{{Binding IsHistoryPanelOpen}}"/>
      </Grid>

      <Viewbox x:Name="DispBox" Grid.Row="1" Height="80" StretchDirection="DownOnly" HorizontalAlignment="Right" Stretch="Uniform">
        <TextBlock x:Name="DispText" Classes="disp" Classes.err="{{Binding IsError}}" Text="{{Binding DisplayValue}}"
                   TextAlignment="Right" Margin="0,2"/>
      </Viewbox>

      <Grid Grid.Row="2" ColumnDefinitions="*,*,*,*,*,*" Margin="0,2">
        <Button Grid.Column="0" Classes="flat" Content="MC" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding MemoryClearAllCommand}}" IsEnabled="{{Binding HasMemory}}"/>
        <Button Grid.Column="1" Classes="flat" Content="MR" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding MemoryRecallCommand}}" IsEnabled="{{Binding HasMemory}}"/>
        <Button Grid.Column="2" Classes="flat" Content="M+" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding MemoryAddCommand}}"/>
        <Button Grid.Column="3" Classes="flat" Content="M−" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding MemorySubtractCommand}}"/>
        <Button Grid.Column="4" Classes="flat" Content="MS" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding MemoryStoreCommand}}"/>
        <Button Grid.Column="5" Classes="flat" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Command="{{Binding ToggleMemoryCommand}}" Classes.sel="{{Binding IsMemoryPanelOpen}}">
          <StackPanel Orientation="Horizontal" Spacing="1">
            <TextBlock Text="M"/>
            <TextBlock Classes="ico" Text="{{x:Static ic:Icons.CaretDown}}" FontSize="12" VerticalAlignment="Center"/>
          </StackPanel>
        </Button>
      </Grid>

      <StackPanel Grid.Row="3" IsVisible="{{Binding IsProgrammer}}" Spacing="0" Margin="0,2">
{radix_row("HEX", "HexText", "IsHexRadix", "Hex")}
{radix_row("DEC", "DecText", "IsDecRadix", "Decimal")}
{radix_row("OCT", "OctText", "IsOctRadix", "Octal")}
{radix_row("BIN", "BinText", "IsBinRadix", "Binary")}
      </StackPanel>
    </Grid>

    <!-- Keypads -->
    <Grid Grid.Row="1" x:Name="PadHost" ColumnDefinitions="*,*" RowDefinitions="*,*">
      <Grid x:Name="SidePanel" IsVisible="{{Binding HasSidePanel}}">
        <UniformGrid Columns="5" IsVisible="{{Binding IsScientific}}">
{block(sci)}
        </UniformGrid>
        <UniformGrid Columns="5" IsVisible="{{Binding IsProgrammerPanelVisible}}">
{block(prog)}
        </UniformGrid>
        <Grid IsVisible="{{Binding IsBitPanelVisible}}" RowDefinitions="*,Auto">
          <ItemsControl ItemsSource="{{Binding Bits}}" VerticalAlignment="Center">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate>
                <UniformGrid Columns="{{Binding $parent[UserControl].DataContext.BitColumns}}"/>
              </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Button Classes="key digit small" Classes.sel="{{Binding IsSet}}" Content="{{Binding Text}}" Height="40"
                        Command="{{Binding $parent[UserControl].DataContext.ToggleBitCommand}}" CommandParameter="{{Binding}}"/>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
          <UniformGrid Grid.Row="1" Columns="3" Height="44">
            <Button Classes="key small" Content="123" Command="{{Binding ToggleBitFlipCommand}}"/>
            <Button Classes="key small" Command="{{Binding CycleWordSizeCommand}}" Content="{{Binding WordSizeLabel}}"/>
            <Button Classes="key small accent" Content="C" Command="{{Binding PressCommand}}" CommandParameter="{{x:Static e:Command.CommandCLEAR}}"/>
          </UniformGrid>
        </Grid>
      </Grid>

      <UniformGrid x:Name="MainPad" Columns="4" IsVisible="{{Binding IsKeypadVisible}}">
{block(main)}
      </UniformGrid>
    </Grid>

    <!-- History / memory flyout -->
    <Border x:Name="HistoryFlyout" Grid.Row="1" IsVisible="{{Binding IsAnyPanelOpen}}" Background="{{DynamicResource OverlayBg}}"
            BorderBrush="{{DynamicResource PanelBorder}}" BorderThickness="1" CornerRadius="6" Padding="6">
      <Grid RowDefinitions="Auto,*">
        <Grid Grid.Row="0" ColumnDefinitions="*,Auto">
          <TextBlock Classes="title" FontSize="16" VerticalAlignment="Center" Margin="4,0"
                     Text="{{loc:T Key=History}}" IsVisible="{{Binding IsHistoryPanelOpen}}"/>
          <TextBlock Classes="title" FontSize="16" VerticalAlignment="Center" Margin="4,0"
                     Text="{{loc:T Key=Memory}}" IsVisible="{{Binding IsMemoryPanelOpen}}"/>
          <Button Grid.Column="1" Classes="flat ico" Content="{{x:Static ic:Icons.Trash}}" IsVisible="{{Binding IsHistoryPanelOpen}}"
                  ToolTip.Tip="{{loc:T Key=ClearHistory}}" Command="{{Binding HistoryClearCommand}}"/>
          <Button Grid.Column="1" Classes="flat ico" Content="{{x:Static ic:Icons.Trash}}" IsVisible="{{Binding IsMemoryPanelOpen}}"
                  ToolTip.Tip="{{loc:T Key=ClearMemory}}" Command="{{Binding MemoryClearAllCommand}}"/>
        </Grid>

        <Grid Grid.Row="1" IsVisible="{{Binding IsHistoryPanelOpen}}">
          <TextBlock Classes="sub" Text="{{loc:T Key=HistoryEmpty}}" IsVisible="{{Binding IsHistoryEmpty}}"
                     TextWrapping="Wrap" Margin="6,12"/>
          <ScrollViewer>
            <ItemsControl ItemsSource="{{Binding HistoryItems}}">
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <Grid ColumnDefinitions="*,Auto">
                    <Button Classes="flat" HorizontalAlignment="Stretch" HorizontalContentAlignment="Right"
                            Command="{{Binding $parent[UserControl].DataContext.HistoryItemSelectCommand}}"
                            CommandParameter="{{Binding}}">
                      <StackPanel>
                        <TextBlock Classes="sub" Text="{{Binding Expression}}" FontSize="13" TextAlignment="Right" TextWrapping="Wrap"/>
                        <TextBlock Text="{{Binding Result}}" FontSize="22" FontWeight="SemiBold" TextAlignment="Right" TextWrapping="Wrap"/>
                      </StackPanel>
                    </Button>
                    <Button Grid.Column="1" Classes="flat ico" Content="{{x:Static ic:Icons.X}}" VerticalAlignment="Center"
                            Command="{{Binding $parent[UserControl].DataContext.HistoryItemDeleteCommand}}"
                            CommandParameter="{{Binding}}"/>
                  </Grid>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
          </ScrollViewer>
        </Grid>

        <Grid Grid.Row="1" IsVisible="{{Binding IsMemoryPanelOpen}}">
          <TextBlock Classes="sub" Text="{{loc:T Key=MemoryEmpty}}" IsVisible="{{Binding IsMemoryEmpty}}"
                     TextWrapping="Wrap" Margin="6,12"/>
          <ScrollViewer>
            <ItemsControl ItemsSource="{{Binding MemoryItems}}">
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <Grid ColumnDefinitions="*" Margin="0,4">
                    <StackPanel>
                      <Button Classes="flat" HorizontalAlignment="Stretch" HorizontalContentAlignment="Right"
                              Command="{{Binding $parent[UserControl].DataContext.MemoryItemLoadCommand}}"
                              CommandParameter="{{Binding}}">
                        <TextBlock Text="{{Binding Value}}" FontSize="22" FontWeight="SemiBold" TextAlignment="Right" TextWrapping="Wrap"/>
                      </Button>
                      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                        <Button Classes="flat" Content="MC" Command="{{Binding $parent[UserControl].DataContext.MemoryItemClearCommand}}" CommandParameter="{{Binding}}"/>
                        <Button Classes="flat" Content="M+" Command="{{Binding $parent[UserControl].DataContext.MemoryItemAddCommand}}" CommandParameter="{{Binding}}"/>
                        <Button Classes="flat" Content="M−" Command="{{Binding $parent[UserControl].DataContext.MemoryItemSubtractCommand}}" CommandParameter="{{Binding}}"/>
                      </StackPanel>
                    </StackPanel>
                  </Grid>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
          </ScrollViewer>
        </Grid>
      </Grid>
    </Border>
  </Grid>
</UserControl>
'''
open(sys.argv[1], "w", encoding="utf-8").write(xaml)
