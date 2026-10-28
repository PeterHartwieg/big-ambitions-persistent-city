import unittest
from analyze_stalls import analyze, analyze_capture


def observation(start=100, end=200, prior=1, current=2, **extra):
    return dict(startTicks=str(start), endTicks=str(end), priorUnityFrame=str(prior), currentUnityFrame=str(current), **extra)


class StallAnalysisTests(unittest.TestCase):
    def test_event_context_and_signed_heap_delta_survive_analysis(self):
        mod = {'unityFrame': '1', 'slowestCitizen': 'pc:3', 'entryBranch': 'arrival',
               'eventSlices': '2', 'eventMs': '4', 'managedDeltaBytes': '-128',
               'managedDeltaAvailable': '1', 'managedCounterKind': 'mono-used-heap-bytes'}
        result = analyze([observation()], [mod], 1000)
        context = result['events'][0]['neighborModFrames'][0]
        self.assertEqual(context['slowestEvent']['citizen'], 'pc:3')
        self.assertEqual(context['slowestEvent']['eventSlices'], 2)
        self.assertEqual(context['managedDelta']['bytes'], -128)
        self.assertEqual(context['managedDelta']['kind'], 'mono-used-heap-bytes')

    def test_unknown_is_not_zero_and_neighbors_are_context(self):
        row = observation(cpuLatestMs='0', cpuAvailable='true', gpuLatestMs='0', gpuAvailable='false', gcAvailable='true', gc0Delta='1', gc1Delta='0', gc2Delta='0')
        result = analyze([row], [{'unityFrame':'1','combinedUnionMs':'5','Physical.UpdateInclusiveMs':'4'}], 1000)
        e = result['events'][0]
        self.assertEqual(e['wallIntervalMs'], 100)
        self.assertEqual(e['latestTimingObservation']['cpuMs'], 0)
        self.assertIsNone(e['latestTimingObservation']['gpuMs'])
        self.assertIsNone(e['allocationBytesObserved'])
        self.assertEqual(e['gcDeltas'], [1,0,0])
        self.assertEqual(e['neighborModFrames'][0]['combinedUnionMs'], 5)
        self.assertNotIn('unattributedMs', e)
        self.assertEqual(e['latestTimingObservation']['frameOwnership'], 'unknown')

    def test_context_is_bounded_even_across_long_loading_gap(self):
        result = analyze([observation(current=10000)], [], 1000, context=2)
        e = result['events'][0]
        self.assertTrue(e['frameGap'])
        self.assertLessEqual(len(e['missingNeighborModFrames']), 10)

    def test_duplicate_and_backward_frames_rejected(self):
        for rows in ([observation(),observation(200,300)], [observation(200,100)], [observation(prior=3,current=2)]):
            with self.assertRaises(ValueError): analyze(rows, [], 1000)
        with self.assertRaises(ValueError): analyze([], [{'unityFrame':'1'},{'unityFrame':'1'}], 1000)

    def test_gap_and_display_limit_are_reported(self):
        result = analyze([observation(),observation(250,500,2,3)], [], 1000, limit=1)
        self.assertEqual(result['eventsEmitted'], 1)
        self.assertEqual(result['eventsOmittedByDisplayLimit'], 1)
        self.assertTrue(result['events'][0]['intervalDiscontinuity'])
        self.assertEqual(result['maxRetainedWallIntervalMs'], 250)

    def test_empty_and_nonfinite_input(self):
        self.assertIsNone(analyze([], [], 1000)['maxRetainedWallIntervalMs'])
        with self.assertRaises(ValueError): analyze([], [], 0)
        with self.assertRaises(ValueError): analyze([observation(cpuLatestMs='nan',cpuAvailable='1')],[],1000)

    def test_frame_chain_is_checked_separately_from_tick_chain(self):
        with self.assertRaises(ValueError): analyze([observation(prior=2,current=2)],[],1000)
        result = analyze([observation(),observation(200,300,8,9)],[],1000)
        self.assertEqual(result['gappedOrDiscontinuousIntervals'],1)
        self.assertTrue(result['events'][1]['intervalDiscontinuity'])


    def test_capture_report_binds_files_counts_and_observer_flags(self):
        rows = [observation(),observation(200,300,2,3)]
        report = {'observationEvidence': 'C:/test/frames-observations-ID.csv', 'perFrameEvidence':'C:/test/frames-mod-work-ID.csv',
            'rawObservations': {'Count':2,'Capacity':2,'Dropped':3,'stopwatchFrequency':1000,'initialIntervalPartial':True,'outstandingPrimaryScopes':0,'discardedLifecycleTailTicks':125},
            'completedUnityFrames':0, 'stopReason':'frames-stop', 'framesOver50ms':5,
            'diagnosticDispatches':[{'operation':'dump','unityFrame':2,'ticks':210}], 'diagnosticDispatchCount':1}
        result = analyze_capture(rows,[],report,'frames-observations-ID.csv','frames-mod-work-ID.csv')
        self.assertTrue(result['events'][0]['partialStart'])
        self.assertTrue(result['events'][1]['observerContaminated'])
        self.assertEqual(result['events'][1]['diagnosticDispatches'][0]['operation'],'dump')
        self.assertEqual(result['capture']['wholeCaptureMinusRetainedSlowIntervals'],3)
        self.assertEqual(result['capture']['rawRowsDropped'],3)
        self.assertEqual(result['capture']['discardedLifecycleTailTicks'],125)
        report['diagnosticDispatchDropped']=1
        limited = analyze_capture(rows,[],report,'frames-observations-ID.csv','frames-mod-work-ID.csv')
        self.assertFalse(limited['events'][0]['observerContaminationUnknown'])
        self.assertTrue(limited['events'][1]['observerContaminationUnknown'])
        with self.assertRaises(ValueError): analyze_capture(rows,[],report,'frames-observations-OTHER.csv','frames-mod-work-ID.csv')
        report['rawObservations']['Count']=3
        with self.assertRaises(ValueError): analyze_capture(rows,[],report,'frames-observations-ID.csv','frames-mod-work-ID.csv')


if __name__ == '__main__': unittest.main()
