using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views.Controls;

namespace StoryboardDesigner.App.Tests;

public sealed class AreaMapCanvasLayoutTests
{
    [Fact]
    public void MapRendersMixedFootprintsAndThreeSouthArrowsAtDefaultZoom()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var rooms = Enumerable.Range(0, 7).Select(i => new Room { Name = $"Room {i}" }).ToArray();
                var area = new Area
                {
                    Name = "Preview",
                    Rooms = rooms.ToList(),
                    RoomPlacements =
                    [
                        new AreaRoomPlacement { RoomId = rooms[0].Id, X = 24, Y = 24 },
                        new AreaRoomPlacement { RoomId = rooms[1].Id, X = 204, Y = 24, ColumnSpan = 2 },
                        new AreaRoomPlacement { RoomId = rooms[2].Id, X = 24, Y = 164, RowSpan = 3 },
                        new AreaRoomPlacement { RoomId = rooms[3].Id, X = 384, Y = 164, ColumnSpan = 3, RowSpan = 3 },
                        new AreaRoomPlacement { RoomId = rooms[4].Id, X = 384, Y = 584 },
                        new AreaRoomPlacement { RoomId = rooms[5].Id, X = 564, Y = 584 },
                        new AreaRoomPlacement { RoomId = rooms[6].Id, X = 744, Y = 584 }
                    ],
                    TraversalConnections = rooms.Skip(4).Select(room => new TraversalConnection
                    {
                        RoomAId = rooms[3].Id,
                        RoomBId = room.Id,
                        BaseTraversalDirectionFromA = Direction10.South,
                        TraversalAccessMode = TraversalAccessMode.TwoWay
                    }).ToList()
                };
                var editor = new AreaNavigationEditorTabViewModel(area);
                var map = new AreaMapCanvas { DataContext = editor };
                map.Measure(new Size(editor.CanvasWidth, editor.CanvasHeight));
                map.Arrange(new Rect(0, 0, editor.CanvasWidth, editor.CanvasHeight));
                map.UpdateLayout();

                Assert.Equal(1, editor.Zoom);
                Assert.Equal(6, editor.NavigationArrows.Count);
                var bitmap = new RenderTargetBitmap((int)editor.CanvasWidth, (int)editor.CanvasHeight,
                    96, 96, PixelFormats.Pbgra32);
                bitmap.Render(map);

                var previewPath = Environment.GetEnvironmentVariable("STORYBOARD_AREA_MAP_PREVIEW");
                if (!string.IsNullOrWhiteSpace(previewPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(previewPath);
                    encoder.Save(stream);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(MapRendersMixedFootprintsAndThreeSouthArrowsAtDefaultZoom));
        if (failure is not null) throw failure;
    }
}
