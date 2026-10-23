using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenUtau.Core.Pipeline;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core.ExpressionGraph {
    /// <summary>
    /// The driven-curve preview (the dashed line in the piano roll) is drawn from
    /// RenderPhrase.drivenCurves. A curve the graph drives must reach it whether or not
    /// the part already has a curve for that expression.
    /// </summary>
    public class ExpressionGraphDrivenCurveTest {
        const string Renderer = "TEST";

        static UGraphNode Node(int id, string type, params (string key, string value)[] parameters) {
            var node = new UGraphNode { id = id, type = type };
            foreach (var (key, value) in parameters) {
                node.Set(key, value);
            }
            return node;
        }

        static UGraphLink Link(int from, int to, string port = GraphNodeTypes.Value) =>
            new UGraphLink { from = from, to = to, toPort = port };

        static UExpressionGraph Graph(IEnumerable<UGraphNode> nodes, params UGraphLink[] links) =>
            new UExpressionGraph { id = "g", name = "G", renderer = Renderer, nodes = nodes.ToList(), links = links.ToList() };

        [Theory]
        [InlineData("tenc")]
        [InlineData("brec")]
        [InlineData("voic")]
        [InlineData("genc")]
        public void DrivenCurveReachesEveryDrivenExpression(string abbr) {
            var graph = Graph(new[] {
                Node(1, GraphNodeTypes.Time),
                Node(2, GraphNodeTypes.Multiply, ("b", "50")),
                Node(3, GraphNodeTypes.Add, ("b", "10")),
                Node(4, GraphNodeTypes.CurveOutput, ("abbr", abbr)),
            }, Link(1, 2, "a"), Link(2, 3, "a"), Link(3, 4));
            var (project, track, part) = Fixture(graph);
            var phrases = PhraseSource.FromPart(project, track, part, 0).BuildPhrases();
            Assert.NotEmpty(phrases);
            foreach (var phrase in phrases) {
                Assert.NotNull(phrase.drivenCurves);
                Assert.True(phrase.drivenCurves!.ContainsKey(abbr),
                    $"driven curve for \"{abbr}\" is missing. Keys present: " +
                    string.Join(", ", phrase.drivenCurves.Keys));
                Assert.True(phrase.drivenCurves[abbr].Length > 0);
            }
        }

        [Fact]
        public void EveryDrivenCurveOfOneGraphReachesThePhrase() {
            // One graph driving several curves at once, as a real graph does.
            var graph = Graph(new[] {
                Node(1, GraphNodeTypes.Time),
                Node(2, GraphNodeTypes.Multiply, ("b", "50")),
                Node(3, GraphNodeTypes.CurveOutput, ("abbr", "tenc")),
                Node(4, GraphNodeTypes.Add, ("b", "10")),
                Node(5, GraphNodeTypes.CurveOutput, ("abbr", "brec")),
                Node(6, GraphNodeTypes.CurveOutput, ("abbr", "voic")),
                Node(7, GraphNodeTypes.CurveOutput, ("abbr", "genc")),
                Node(8, GraphNodeTypes.CurveOutput, ("abbr", "cstm")),
            }, Link(1, 2, "a"), Link(2, 3),
                Link(2, 4, "a"), Link(4, 5),
                Link(2, 6), Link(2, 7), Link(2, 8));
            var (project, track, part) = Fixture(graph);
            var phrases = PhraseSource.FromPart(project, track, part, 0).BuildPhrases();
            Assert.NotEmpty(phrases);
            foreach (var phrase in phrases) {
                Assert.NotNull(phrase.drivenCurves);
                foreach (var abbr in new[] { "tenc", "brec", "voic", "genc", "cstm" }) {
                    Assert.True(phrase.drivenCurves!.ContainsKey(abbr),
                        $"driven curve for \"{abbr}\" is missing. Keys present: " +
                        string.Join(", ", phrase.drivenCurves.Keys));
                }
            }
        }

        static (UProject project, UTrack track, UVoicePart part) Fixture(UExpressionGraph graph) {
            var (project, track, part) = ExpressionGraphTest.FixtureForTest(graph);
            // The shared fixture only registers a few curve expressions; add the rest of
            // the built-in ones so a graph can drive them.
            project.RegisterExpression(new UExpressionDescriptor("gender (curve)", "genc", -100, 100, 0) {
                type = UExpressionType.Curve,
            });
            project.RegisterExpression(new UExpressionDescriptor("breathiness (curve)", "brec", -100, 100, 0) {
                type = UExpressionType.Curve,
            });
            project.RegisterExpression(new UExpressionDescriptor("voicing (curve)", "voic", 0, 100, 100) {
                type = UExpressionType.Curve,
            });
            return (project, track, part);
        }
    }
}
