(function () {
    window.ArchitectureStudio = {
        cy: null,
        dotNetRef: null,
        rawElements: null,
        activeLayout: 'cose',
        activeLens: 'default',
        expandedNodes: new Set(),
        particleInterval: null,
        isParticleFlowActive: false,

        init: function (containerId, elementsData, dotNetHelper) {
            var container = document.getElementById(containerId);
            if (!container) return;

            this.dotNetRef = dotNetHelper;
            this.rawElements = typeof elementsData === 'string' ? JSON.parse(elementsData) : elementsData;

            if (this.cy) {
                this.cy.destroy();
            }

            var self = this;

            this.cy = cytoscape({
                container: container,
                elements: this.rawElements,
                boxSelectionEnabled: false,
                autounselectify: false,
                minZoom: 0.35,
                maxZoom: 2.5,
                wheelSensitivity: 0.25,
                style: this.getStyles(),
                layout: this.getLayoutConfig(this.activeLayout)
            });

            // Single Node Click -> Open Deep Drilldown Inspector & Highlight Direct Neighbors
            this.cy.on('tap', 'node', function (evt) {
                var node = evt.target;
                if (node.isParent() && node.data('type') === 'Domain') return;

                self.highlightDependencies(node);

                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnStudioNodeSelected', node.id());
                }
            });

            // Double Click -> Instant Deep Drilldown / Focus on Clicked Node
            this.cy.on('dbltap', 'node', function (evt) {
                var node = evt.target;
                if (node.isParent() && node.data('type') === 'Domain') return;

                if (self.cy) {
                    self.cy.animate({
                        center: { eles: node },
                        zoom: Math.min(2.0, self.cy.zoom() * 1.3),
                        duration: 350
                    });
                }

                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnStudioNodeDoubleClicked', node.id());
                }
            });

            // Background Click -> Clear selection & close drawer
            this.cy.on('tap', function (evt) {
                if (evt.target === self.cy) {
                    self.clearHighlights();
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnStudioBackgroundClicked');
                    }
                }
            });

            // Auto-center with comfortable padding once layout is ready
            this.cy.ready(function () {
                self.cy.fit(null, 50);
            });
        },

        getStyles: function () {
            return [
                // 1. Domain Super-Node for Macro View
                {
                    selector: 'node[type="DomainHub"]',
                    style: {
                        'background-color': '#FFFFFF',
                        'border-color': '#562178',
                        'border-width': 3,
                        'shape': 'roundrectangle',
                        'width': 280,
                        'height': 110,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'text-max-width': 260,
                        'color': '#1E293B',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 13,
                        'font-weight': 800,
                        'shadow-blur': 16,
                        'shadow-color': 'rgba(86, 33, 120, 0.25)',
                        'shadow-opacity': 0.8,
                        'shadow-offset-y': 6
                    }
                },
                // 1b. Domain Bounded Context Compound Boxes (Clean Light Swimlanes)
                {
                    selector: 'node[type="Domain"]',
                    style: {
                        'background-color': '#F8FAFC',
                        'border-color': '#CBD5E1',
                        'border-width': 1.5,
                        'border-style': 'dashed',
                        'border-opacity': 0.9,
                        'shape': 'roundrectangle',
                        'label': 'data(label)',
                        'text-valign': 'top',
                        'text-halign': 'center',
                        'text-margin-y': -14,
                        'color': '#475569',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 13,
                        'font-weight': 800,
                        'text-transform': 'uppercase',
                        'letter-spacing': 0.8,
                        'padding': 35
                    }
                },
                // 2. Microservice Core Nodes (Clean Elevated White/Purple Card)
                {
                    selector: 'node[type="Service"]',
                    style: {
                        'background-color': '#FFFFFF',
                        'border-color': '#562178',
                        'border-width': 2.5,
                        'shape': 'roundrectangle',
                        'width': 220,
                        'height': 76,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'text-max-width': 200,
                        'color': '#1E293B',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 12.5,
                        'font-weight': 700,
                        'shadow-blur': 12,
                        'shadow-color': 'rgba(86, 33, 120, 0.2)',
                        'shadow-opacity': 0.8,
                        'shadow-offset-y': 4
                    }
                },
                // 2b. Focal Target Active Center Node
                {
                    selector: 'node.focal-target',
                    style: {
                        'background-color': '#FAF5FF',
                        'border-color': '#562178',
                        'border-width': 4.5,
                        'width': 260,
                        'height': 90,
                        'font-size': 13.5,
                        'font-weight': 800,
                        'shadow-blur': 22,
                        'shadow-color': 'rgba(86, 33, 120, 0.4)',
                        'shadow-opacity': 1,
                        'shadow-offset-y': 6,
                        'z-index': 1000
                    }
                },
                // 3. API Gateway Node (Royal Purple / Gold Accent Diamond)
                {
                    selector: 'node[type="Gateway"]',
                    style: {
                        'background-color': '#FAF5FF',
                        'border-color': '#F8A719',
                        'border-width': 3,
                        'shape': 'diamond',
                        'width': 180,
                        'height': 75,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'color': '#562178',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 12,
                        'font-weight': 800,
                        'shadow-blur': 10,
                        'shadow-color': 'rgba(248, 167, 25, 0.3)'
                    }
                },
                // 4. Persistence & Datastores (Golden Amber Cylinders)
                {
                    selector: 'node[type="Database"]',
                    style: {
                        'background-color': '#FFFDF7',
                        'border-color': '#F8A719',
                        'border-width': 2,
                        'shape': 'barrel',
                        'width': 160,
                        'height': 60,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'text-max-width': 140,
                        'color': '#92400E',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 11,
                        'font-weight': 700,
                        'shadow-blur': 8,
                        'shadow-color': 'rgba(248, 167, 25, 0.15)'
                    }
                },
                // 5. External APIs / Cloud (Emerald Rounded Box)
                {
                    selector: 'node[type="ExternalApi"]',
                    style: {
                        'background-color': '#F0FDF4',
                        'border-color': '#10B981',
                        'border-width': 2,
                        'shape': 'roundrectangle',
                        'width': 160,
                        'height': 56,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'text-max-width': 145,
                        'color': '#065F46',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 11,
                        'font-weight': 700
                    }
                },
                // 6. Event Streams / Topics / Queues (Amber Cut Rectangle)
                {
                    selector: 'node[type="EventTopic"]',
                    style: {
                        'background-color': '#FFFBEB',
                        'border-color': '#F59E0B',
                        'border-width': 2,
                        'shape': 'roundrectangle',
                        'width': 150,
                        'height': 52,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'color': '#B45309',
                        'font-family': 'JetBrains Mono, monospace',
                        'font-size': 10.5,
                        'font-weight': 700
                    }
                },
                // 7. Aggregated Package Bundle Node
                {
                    selector: 'node[type="PackageBundle"]',
                    style: {
                        'background-color': '#FFF1F2',
                        'border-color': '#E11D48',
                        'border-width': 2,
                        'shape': 'roundrectangle',
                        'width': 180,
                        'height': 54,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'color': '#9F1239',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 11,
                        'font-weight': 700
                    }
                },
                // 7b. Supply Chain Packages (Coral Hexagon)
                {
                    selector: 'node[type="KeyPackage"]',
                    style: {
                        'background-color': '#FFF1F2',
                        'border-color': '#FF7675',
                        'border-width': 1.5,
                        'shape': 'hexagon',
                        'width': 130,
                        'height': 42,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'color': '#BE123C',
                        'font-family': 'JetBrains Mono, monospace',
                        'font-size': 10,
                        'font-weight': 600
                    }
                },
                // 8. Internal C4 Components (Light Purple Rectangles)
                {
                    selector: 'node[type="Component"]',
                    style: {
                        'background-color': '#F3E8FF',
                        'border-color': '#A855F7',
                        'border-width': 1.5,
                        'shape': 'roundrectangle',
                        'width': 140,
                        'height': 44,
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-wrap': 'wrap',
                        'color': '#6B21A8',
                        'font-family': 'Inter, sans-serif',
                        'font-size': 10,
                        'font-weight': 600
                    }
                },
                // Edges & Flows
                {
                    selector: 'edge',
                    style: {
                        'width': 2.2,
                        'line-color': '#CBD5E1',
                        'target-arrow-color': '#94A3B8',
                        'target-arrow-shape': 'triangle',
                        'curve-style': 'bezier',
                        'label': 'data(label)',
                        'font-size': 9.5,
                        'font-family': 'JetBrains Mono, monospace',
                        'color': '#475569',
                        'text-background-color': '#FFFFFF',
                        'text-background-opacity': 0.95,
                        'text-background-padding': 3,
                        'text-background-shape': 'roundrectangle',
                        'text-border-color': '#E2E8F0',
                        'text-border-width': 1,
                        'text-rotation': 'autorotate'
                    }
                },
                // Edge Type Variations
                {
                    selector: 'edge[type="EventStream"], edge[label*="MQTT"], edge[label*="Event"]',
                    style: {
                        'line-style': 'dashed',
                        'line-color': '#F59E0B',
                        'target-arrow-color': '#F59E0B'
                    }
                },
                {
                    selector: 'edge[type="Datastore"], edge[label*="PERSISTS"], edge[label*="READS"]',
                    style: {
                        'line-color': '#F8A719',
                        'target-arrow-color': '#F8A719'
                    }
                },
                {
                    selector: 'edge[type="ExternalApi"], edge[label*="CALLS"]',
                    style: {
                        'line-color': '#10B981',
                        'target-arrow-color': '#10B981'
                    }
                },
                // Highlighted Dependency Neighbors
                {
                    selector: '.highlighted',
                    style: {
                        'border-color': '#562178',
                        'border-width': 3.5,
                        'line-color': '#562178',
                        'target-arrow-color': '#562178',
                        'opacity': 1,
                        'z-index': 999
                    }
                },
                // 1st-Degree Direct Dependencies
                {
                    selector: '.direct-dep',
                    style: {
                        'border-color': '#F8A719',
                        'border-width': 3,
                        'line-color': '#F8A719',
                        'target-arrow-color': '#F8A719',
                        'opacity': 1,
                        'z-index': 900
                    }
                },
                // Dimmed Non-Connected Nodes
                {
                    selector: '.dimmed',
                    style: {
                        'opacity': 0.15
                    }
                },
                // Outage / Failure Source Node
                {
                    selector: '.outage-source',
                    style: {
                        'background-color': '#FEE2E2',
                        'border-color': '#EF4444',
                        'border-width': 4,
                        'color': '#991B1B',
                        'font-weight': 900,
                        'shadow-blur': 15,
                        'shadow-color': '#EF4444',
                        'shadow-opacity': 0.8
                    }
                },
                // Outage Cascading Impacted Downstream Nodes
                {
                    selector: '.outage-cascade',
                    style: {
                        'border-color': '#EF4444',
                        'border-width': 2.8,
                        'line-color': '#EF4444',
                        'target-arrow-color': '#EF4444',
                        'line-style': 'dashed',
                        'opacity': 1
                    }
                },
                // LENS: STRIDE Threats Heatmap Overlays
                {
                    selector: '.lens-threat-critical',
                    style: {
                        'background-color': '#FEF2F2',
                        'border-color': '#DC2626',
                        'border-width': 3
                    }
                },
                {
                    selector: '.lens-threat-medium',
                    style: {
                        'background-color': '#FFFBEB',
                        'border-color': '#F59E0B',
                        'border-width': 2.5
                    }
                },
                {
                    selector: '.lens-threat-clean',
                    style: {
                        'background-color': '#F0FDF4',
                        'border-color': '#16A34A',
                        'border-width': 2
                    }
                },
                // LENS: Quality Rating Stars
                {
                    selector: '.lens-quality-high',
                    style: {
                        'border-color': '#10B981',
                        'border-width': 2.8
                    }
                },
                {
                    selector: '.lens-quality-low',
                    style: {
                        'border-color': '#F59E0B',
                        'border-width': 2.8
                    }
                }
            ];
        },

        getLayoutConfig: function (layoutName) {
            switch (layoutName) {
                case 'focal-flow':
                    return {
                        name: 'dagre',
                        rankDir: 'LR',
                        nodeSep: 45,
                        rankSep: 180,
                        padding: 50,
                        spacingFactor: 1.2,
                        animate: true,
                        animationDuration: 500
                    };
                case 'dagre-lr':
                    return {
                        name: 'dagre',
                        rankDir: 'LR',
                        nodeSep: 60,
                        rankSep: 120,
                        padding: 50,
                        spacingFactor: 1.2,
                        animate: true,
                        animationDuration: 500
                    };
                case 'concentric':
                    return {
                        name: 'concentric',
                        minNodeSpacing: 80,
                        padding: 50,
                        concentric: function (node) {
                            if (node.data('type') === 'DomainHub') return 6;
                            if (node.data('type') === 'Gateway') return 5;
                            if (node.data('type') === 'Service') return 4;
                            if (node.data('type') === 'Database') return 3;
                            if (node.data('type') === 'ExternalApi') return 2;
                            return 1;
                        },
                        levelWidth: function () { return 1; },
                        animate: true,
                        animationDuration: 500
                    };
                case 'cose':
                case 'physics':
                default:
                    return {
                        name: 'cose',
                        animate: true,
                        animationDuration: 700,
                        nodeRepulsion: 220000,
                        nodeOverlap: 30,
                        idealEdgeLength: 140,
                        edgeElasticity: 0.45,
                        nestingFactor: 1.2,
                        gravity: 0.25,
                        numIter: 1000,
                        initialTemp: 200,
                        coolingFactor: 0.95,
                        minTemp: 1.0,
                        padding: 50
                    };
                case 'circle':
                    return {
                        name: 'circle',
                        padding: 50,
                        animate: true,
                        animationDuration: 500
                    };
                case 'dagre-tb':
                    return {
                        name: 'dagre',
                        rankDir: 'TB',
                        nodeSep: 80,
                        rankSep: 120,
                        padding: 50,
                        spacingFactor: 1.3,
                        animate: true,
                        animationDuration: 500
                    };
            }
        },

        setLayout: function (layoutName) {
            if (!this.cy) return;
            this.activeLayout = layoutName;
            var layout = this.cy.layout(this.getLayoutConfig(layoutName));
            layout.run();
        },

        setLens: function (lensName) {
            if (!this.cy) return;
            this.activeLens = lensName;
            this.clearHighlights();

            var self = this;
            this.cy.nodes().removeClass('lens-threat-critical lens-threat-medium lens-threat-clean lens-quality-high lens-quality-low');

            if (lensName === 'threats') {
                this.cy.nodes().forEach(function (node) {
                    var threats = node.data('threatCount') || 0;
                    if (threats >= 5) node.addClass('lens-threat-critical');
                    else if (threats > 0) node.addClass('lens-threat-medium');
                    else if (node.data('type') === 'Service') node.addClass('lens-threat-clean');
                });
            } else if (lensName === 'quality') {
                this.cy.nodes().forEach(function (node) {
                    var stars = node.data('qualityStars') || 0;
                    if (stars >= 4.0) node.addClass('lens-quality-high');
                    else if (stars > 0) node.addClass('lens-quality-low');
                });
            }
        },

        highlightDependencies: function (node) {
            this.clearHighlights();
            var direct = node.neighborhood();
            var secondDegree = direct.neighborhood();
            var allConnected = node.union(direct).union(secondDegree);

            this.cy.elements().addClass('dimmed');
            allConnected.removeClass('dimmed');
            node.addClass('highlighted');
            direct.addClass('direct-dep');
        },

        clearHighlights: function () {
            if (this.cy) {
                this.cy.elements().removeClass('dimmed highlighted direct-dep outage-source outage-cascade');
            }
        },

        searchAndFocus: function (query) {
            if (!this.cy) return;
            if (!query || query.trim() === '') {
                this.clearHighlights();
                this.zoomToFit();
                return;
            }

            var q = query.toLowerCase().trim();
            var matches = this.cy.nodes().filter(function (node) {
                if (node.isParent() && node.data('type') === 'Domain') return false;
                var name = (node.data('name') || node.id()).toLowerCase();
                var tier = (node.data('tier') || '').toLowerCase();
                var purpose = (node.data('purpose') || '').toLowerCase();
                return name.includes(q) || tier.includes(q) || purpose.includes(q);
            });

            if (matches.length > 0) {
                this.cy.elements().addClass('dimmed');
                matches.removeClass('dimmed').addClass('highlighted');
                matches.neighborhood().removeClass('dimmed').addClass('direct-dep');

                this.cy.animate({
                    fit: {
                        eles: matches,
                        padding: 100
                    },
                    duration: 500
                });
            }
        },

        filterByProtocol: function (protocol) {
            if (!this.cy) return;
            if (!protocol || protocol === 'All') {
                this.clearHighlights();
                this.zoomToFit();
                return;
            }

            var matchingEdges = this.cy.edges().filter(function (edge) {
                var label = (edge.data('label') || '').toLowerCase();
                var type = (edge.data('type') || '').toLowerCase();
                var p = protocol.toLowerCase();
                return label.includes(p) || type.includes(p);
            });

            var matchingNodes = matchingEdges.connectedNodes();
            var subTree = matchingEdges.union(matchingNodes);

            this.cy.elements().addClass('dimmed');
            subTree.removeClass('dimmed').addClass('highlighted');

            this.cy.animate({
                fit: { eles: subTree, padding: 80 },
                duration: 500
            });
        },

        filterByTier: function (tier) {
            if (!this.cy) return;
            if (!tier || tier === 'All') {
                this.clearHighlights();
                this.zoomToFit();
                return;
            }

            var matchingNodes = this.cy.nodes().filter(function (node) {
                return (node.data('type') || '').toLowerCase() === tier.toLowerCase()
                    || (node.data('tier') || '').toLowerCase() === tier.toLowerCase();
            });

            var subElements = matchingNodes.union(matchingNodes.connectedEdges());
            this.cy.elements().addClass('dimmed');
            subElements.removeClass('dimmed').addClass('highlighted');

            this.cy.animate({
                fit: { eles: subElements, padding: 80 },
                duration: 500
            });
        },

        filterByDomain: function (domainName) {
            if (!this.cy) return;
            if (!domainName || domainName === 'All') {
                this.clearHighlights();
                this.zoomToFit();
                return;
            }

            var domainNodes = this.cy.nodes().filter(function (node) {
                return node.data('domain') === domainName || node.id() === 'gateway';
            });

            var subElements = domainNodes.union(domainNodes.connectedEdges());
            this.cy.elements().addClass('dimmed');
            subElements.removeClass('dimmed').addClass('highlighted');

            this.cy.animate({
                fit: { eles: subElements, padding: 80 },
                duration: 500
            });
        },

        simulateOutageCascade: function (nodeId) {
            if (!this.cy) return;
            this.clearHighlights();

            var sourceNode = this.cy.getElementById(nodeId);
            if (!sourceNode || sourceNode.length === 0) return;

            sourceNode.addClass('outage-source');

            // Find all downstream predecessors / callers that rely on this node
            var affectedIncoming = sourceNode.incomers();
            var affectedOutgoing = sourceNode.successors();
            var affectedTree = sourceNode.union(affectedIncoming).union(affectedOutgoing);

            affectedIncoming.addClass('outage-cascade');
            affectedOutgoing.addClass('outage-cascade');

            this.cy.elements().addClass('dimmed');
            affectedTree.removeClass('dimmed');

            this.cy.animate({
                fit: { eles: affectedTree, padding: 100 },
                duration: 600
            });
        },

        toggleCompoundExpansion: function (serviceId) {
            if (!this.cy) return;
            var isExpanded = this.expandedNodes.has(serviceId);

            if (isExpanded) {
                this.expandedNodes.delete(serviceId);
                // Hide component children
                this.cy.nodes().filter(function (n) {
                    return n.data('parent') === serviceId && n.data('type') === 'Component';
                }).style('display', 'none');
            } else {
                this.expandedNodes.add(serviceId);
                // Show component children
                this.cy.nodes().filter(function (n) {
                    return n.data('parent') === serviceId && n.data('type') === 'Component';
                }).style('display', 'element');
            }

            this.setLayout(this.activeLayout);
        },

        toggleParticleFlow: function () {
            this.isParticleFlowActive = !this.isParticleFlowActive;
            if (this.isParticleFlowActive) {
                this.startParticleAnimation();
            } else {
                this.stopParticleAnimation();
            }
            return this.isParticleFlowActive;
        },

        startParticleAnimation: function () {
            if (!this.cy) return;
            var edges = this.cy.edges();
            var offset = 0;

            this.particleInterval = setInterval(function () {
                offset = (offset + 1) % 20;
                edges.style('line-dash-offset', -offset);
            }, 50);
        },

        stopParticleAnimation: function () {
            if (this.particleInterval) {
                clearInterval(this.particleInterval);
                this.particleInterval = null;
            }
        },

        zoomIn: function () {
            if (this.cy) {
                this.cy.zoom(this.cy.zoom() * 1.25);
            }
        },

        zoomOut: function () {
            if (this.cy) {
                this.cy.zoom(this.cy.zoom() * 0.8);
            }
        },

        zoomToFit: function () {
            if (this.cy) {
                this.cy.animate({
                    fit: { padding: 40 },
                    duration: 400
                });
            }
        },

        toggleFullscreen: function (containerId) {
            var el = document.getElementById(containerId);
            if (!el) return;

            if (!document.fullscreenElement) {
                if (el.requestFullscreen) el.requestFullscreen();
                else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
            } else {
                if (document.exitFullscreen) document.exitFullscreen();
            }
        },

        exportPng: function () {
            if (!this.cy) return;
            var png64 = this.cy.png({ full: true, bg: '#FFFFFF', scale: 2 });
            var a = document.createElement('a');
            a.href = png64;
            a.download = 'enterprise_application_dependencies.png';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
        },

        exportSvg: function () {
            if (!this.cy) return;
            var svgContent = this.cy.svg({ full: true, bg: '#FFFFFF', scale: 1 });
            window.downloadFileFromText('enterprise_architecture_topology.svg', svgContent);
        },

        addElements: function (elementsData) {
            if (!this.cy) return;
            var eles = typeof elementsData === 'string' ? JSON.parse(elementsData) : elementsData;
            this.cy.add(eles);
            this.setLayout(this.activeLayout);
        },

        removeElement: function (id) {
            if (!this.cy) return;
            var el = this.cy.getElementById(id);
            if (el && el.length > 0) {
                this.cy.remove(el);
            }
        },

        clearCanvas: function () {
            if (!this.cy) return;
            this.cy.elements().remove();
        },

        exportMermaidC4: function () {
            if (!this.cy) return '';
            var lines = ['C4Context', '    title Enterprise Architecture Diagram', ''];
            var nodes = this.cy.nodes();
            var edges = this.cy.edges();

            nodes.forEach(function (n) {
                if (n.isParent() && n.data('type') === 'Domain') return;
                var id = n.id().replace(/[^a-zA-Z0-9_]/g, '_');
                var label = (n.data('name') || n.id()).replace(/"/g, "'");
                var tier = (n.data('tier') || n.data('type') || 'Component').replace(/"/g, "'");
                var desc = (n.data('purpose') || '').replace(/"/g, "'");

                if (n.data('type') === 'Database') {
                    lines.push('    ContainerDb(' + id + ', "' + label + '", "' + tier + '", "' + desc + '")');
                } else if (n.data('type') === 'ExternalApi') {
                    lines.push('    System_Ext(' + id + ', "' + label + '", "' + desc + '")');
                } else {
                    lines.push('    Container(' + id + ', "' + label + '", "' + tier + '", "' + desc + '")');
                }
            });

            lines.push('');
            edges.forEach(function (e) {
                var src = e.source().id().replace(/[^a-zA-Z0-9_]/g, '_');
                var tgt = e.target().id().replace(/[^a-zA-Z0-9_]/g, '_');
                var label = (e.data('label') || 'Interacts with').replace(/"/g, "'");
                lines.push('    Rel(' + src + ', ' + tgt + ', "' + label + '")');
            });

            return lines.join('\n');
        }
    };

    window.downloadFileFromText = function (filename, textContent) {
        var blob = new Blob([textContent], { type: 'text/plain;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };
})();
